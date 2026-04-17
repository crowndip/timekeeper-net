using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;

namespace ParentalControl.WebService.Services;

public interface IDatabaseInitializationService
{
    Task<bool> IsDatabaseInitializedAsync();
    Task InitializeDatabaseAsync();
}

public class DatabaseInitializationService : IDatabaseInitializationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(AppDbContext context, ILogger<DatabaseInitializationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsDatabaseInitializedAsync()
    {
        try
        {
            await _context.Database.CanConnectAsync();
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
}
