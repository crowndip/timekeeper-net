using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;

namespace ParentalControl.WebService.Services;

public class ManualMigrationService
{
    private readonly AppDbContext _context;

    public ManualMigrationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ApplyAddUserAliasesMigrationAsync()
    {
        try
        {
            // Check if migration already applied
            var hasColumn = await _context.Database.ExecuteSqlRawAsync(
                "SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'PrimaryUserId'");
            
            if (hasColumn > 0)
            {
                return true; // Already applied
            }

            // Apply migration
            await _context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE ""Users"" ADD COLUMN ""PrimaryUserId"" uuid NULL;
                
                CREATE INDEX ""IX_Users_PrimaryUserId"" ON ""Users"" (""PrimaryUserId"");
                
                ALTER TABLE ""Users"" ADD CONSTRAINT ""FK_Users_Users_PrimaryUserId"" 
                    FOREIGN KEY (""PrimaryUserId"") REFERENCES ""Users"" (""Id"") 
                    ON DELETE RESTRICT;
                
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES ('20260420082000_AddUserAliases', '8.0.26')
                ON CONFLICT DO NOTHING;
            ");

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckIfMigrationNeededAsync()
    {
        try
        {
            var result = await _context.Database.ExecuteSqlRawAsync(
                "SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'PrimaryUserId' LIMIT 1");
            return result == 0; // Migration needed if column doesn't exist
        }
        catch
        {
            return false;
        }
    }
}
