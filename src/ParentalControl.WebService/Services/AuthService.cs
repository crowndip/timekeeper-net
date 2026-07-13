using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace ParentalControl.WebService.Services;

public class AuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string AuthSessionKey = "IsAuthenticated";

    // A single shared admin password with unlimited attempts is brute-forceable. This is
    // a simple per-IP in-memory throttle -- intentionally not distributed/persistent,
    // since this service runs as a single instance for one household.
    private const int MaxFailuresBeforeLockout = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<string, FailureRecord> FailuresByIp = new();

    // Every unique "IP" (spoofable via X-Forwarded-For if TrustForwardedHeaders is
    // misconfigured -- see the setting's docs) adds a permanent entry here forever unless
    // pruned. Sweep on the size threshold rather than on a timer: this is a home server
    // with occasional login attempts, not a service worth running a background job for.
    private const int MaxTrackedIps = 1000;
    private static readonly TimeSpan StaleRecordThreshold = TimeSpan.FromHours(1);

    public AuthService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool ValidatePassword(string password)
    {
        var configPassword = _configuration["LimitAdministratorPassword"];
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(configPassword))
            return false;

        // Constant-time comparison so response timing doesn't leak how many leading
        // characters of a guessed password were correct.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(configPassword));
    }

    public bool IsLockedOut(string ipAddress, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!FailuresByIp.TryGetValue(ipAddress, out var record))
            return false;

        lock (record)
        {
            if (record.LockedUntil <= DateTime.UtcNow)
                return false;

            retryAfter = record.LockedUntil - DateTime.UtcNow;
            return true;
        }
    }

    public void RecordLoginFailure(string ipAddress)
    {
        var record = FailuresByIp.GetOrAdd(ipAddress, _ => new FailureRecord());
        lock (record)
        {
            record.Count++;
            record.LastActivity = DateTime.UtcNow;
            if (record.Count >= MaxFailuresBeforeLockout)
            {
                record.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
                record.Count = 0;
            }
        }

        PruneStaleFailureRecordsIfNeeded();
    }

    private static void PruneStaleFailureRecordsIfNeeded()
    {
        if (FailuresByIp.Count <= MaxTrackedIps) return;

        var cutoff = DateTime.UtcNow - StaleRecordThreshold;
        foreach (var kvp in FailuresByIp)
        {
            DateTime lastActivity;
            lock (kvp.Value) { lastActivity = kvp.Value.LastActivity; }

            if (lastActivity < cutoff)
                FailuresByIp.TryRemove(kvp.Key, out _);
        }
    }

    public void RecordLoginSuccess(string ipAddress)
    {
        FailuresByIp.TryRemove(ipAddress, out _);
    }

    public void SetAuthenticated()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.SetString(AuthSessionKey, "true");
        }
    }

    public bool IsAuthenticated()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        return session?.GetString(AuthSessionKey) == "true";
    }

    public void ClearAuthentication()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        session?.Clear();
    }

    private class FailureRecord
    {
        public int Count;
        public DateTime LockedUntil;
        public DateTime LastActivity = DateTime.UtcNow;
    }
}
