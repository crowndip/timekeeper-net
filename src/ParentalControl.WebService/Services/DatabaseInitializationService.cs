using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;

namespace ParentalControl.WebService.Services;

public interface IDatabaseInitializationService
{
    Task<bool> IsDatabaseInitializedAsync();
    Task<bool> CanConnectToDatabaseAsync();
    Task InitializeDatabaseAsync();
    string GetConnectionString();
}

public class DatabaseInitializationService : IDatabaseInitializationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseInitializationService> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseInitializationService(AppDbContext context, ILogger<DatabaseInitializationService> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<bool> CanConnectToDatabaseAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsDatabaseInitializedAsync()
    {
        try
        {
            if (!await CanConnectToDatabaseAsync())
                return false;
            
            return await _context.Database.GetPendingMigrationsAsync().ContinueWith(t => !t.Result.Any());
        }
        catch
        {
            return false;
        }
    }

    public async Task InitializeDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("Initializing database...");
            await _context.Database.MigrateAsync();
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    public string GetConnectionString()
    {
        var connString = _configuration.GetConnectionString("DefaultConnection") ?? "Not configured";
        // Mask password
        var password = _configuration["DbPassword"];
        if (!string.IsNullOrEmpty(password))
        {
            connString = connString.Replace(password, "***");
        }
        return connString;
    }
}
