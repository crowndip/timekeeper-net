using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParentalControl.WebService.Data;

namespace ParentalControl.WebService.Services;

public interface IDatabaseInitializationService
{
    Task<bool> IsDatabaseInitializedAsync();
    Task<bool> CanConnectToDatabaseAsync();
    Task<DatabaseConnectionResult> CheckConnectionAsync();
    Task InitializeDatabaseAsync();
    string GetConnectionString();
}

public class DatabaseConnectionResult
{
    public bool CanConnect { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Suggestion { get; set; }
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

    public async Task<DatabaseConnectionResult> CheckConnectionAsync()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            return new DatabaseConnectionResult
            {
                CanConnect = canConnect,
                ErrorType = canConnect ? null : "Unknown",
                ErrorMessage = canConnect ? null : "Cannot connect to database",
                Suggestion = canConnect ? null : "Check if database server is running"
            };
        }
        catch (NpgsqlException ex) when (ex.Message.Contains("authentication") || ex.Message.Contains("password"))
        {
            return new DatabaseConnectionResult
            {
                CanConnect = false,
                ErrorType = "Authentication",
                ErrorMessage = "Authentication failed - invalid username or password",
                Suggestion = "Check the username and password in your connection string"
            };
        }
        catch (NpgsqlException ex) when (ex.Message.Contains("does not exist") && ex.Message.Contains("database"))
        {
            return new DatabaseConnectionResult
            {
                CanConnect = false,
                ErrorType = "DatabaseNotFound",
                ErrorMessage = "Database does not exist",
                Suggestion = "Create the database first: CREATE DATABASE parental_control;"
            };
        }
        catch (NpgsqlException ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("could not connect"))
        {
            return new DatabaseConnectionResult
            {
                CanConnect = false,
                ErrorType = "ConnectionRefused",
                ErrorMessage = "Connection refused - server not reachable",
                Suggestion = "Check if PostgreSQL is running and the host/port are correct"
            };
        }
        catch (TimeoutException)
        {
            return new DatabaseConnectionResult
            {
                CanConnect = false,
                ErrorType = "Timeout",
                ErrorMessage = "Connection timeout",
                Suggestion = "Check network connectivity and firewall settings"
            };
        }
        catch (Exception ex)
        {
            return new DatabaseConnectionResult
            {
                CanConnect = false,
                ErrorType = "Unknown",
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Suggestion = "Check the logs for more details"
            };
        }
    }

    public async Task<bool> CanConnectToDatabaseAsync()
    {
        var result = await CheckConnectionAsync();
        return result.CanConnect;
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
        catch (NpgsqlException ex) when (ex.Message.Contains("permission") || ex.Message.Contains("denied"))
        {
            _logger.LogError(ex, "Insufficient permissions to initialize database");
            throw new InvalidOperationException("Insufficient permissions. The database user needs CREATE TABLE and CREATE SCHEMA rights.", ex);
        }
        catch (NpgsqlException ex) when (ex.Message.Contains("disk") || ex.Message.Contains("space"))
        {
            _logger.LogError(ex, "Disk space issue during database initialization");
            throw new InvalidOperationException("Disk space full on database server. Free up space and try again.", ex);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Data integrity error during initialization");
            throw new InvalidOperationException("Database initialization failed due to data integrity error. The database may need to be reset.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw new InvalidOperationException($"Database initialization failed: {ex.Message}", ex);
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
