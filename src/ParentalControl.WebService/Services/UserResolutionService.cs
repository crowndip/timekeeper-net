using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Models;

namespace ParentalControl.WebService.Services;

public interface IUserResolutionService
{
    Task<User?> ResolveToPrimaryAsync(Guid userId);
    Task<User?> ResolveToPrimaryAsync(string username);
    Task<List<Guid>> GetAllUserIdsInGroupAsync(Guid primaryUserId);
    Task<bool> CanBecomeAliasAsync(Guid userId);
}

public class UserResolutionService : IUserResolutionService
{
    private readonly AppDbContext _context;

    public UserResolutionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> ResolveToPrimaryAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.PrimaryUser)
            .FirstOrDefaultAsync(u => u.Id == userId);
        
        if (user == null) return null;
        
        return user.PrimaryUserId.HasValue ? user.PrimaryUser : user;
    }

    public async Task<User?> ResolveToPrimaryAsync(string username)
    {
        var user = await _context.Users
            .Include(u => u.PrimaryUser)
            .FirstOrDefaultAsync(u => u.Username == username);
        
        if (user == null) return null;
        
        return user.PrimaryUserId.HasValue ? user.PrimaryUser : user;
    }

    public async Task<List<Guid>> GetAllUserIdsInGroupAsync(Guid primaryUserId)
    {
        var aliases = await _context.Users
            .Where(u => u.PrimaryUserId == primaryUserId)
            .Select(u => u.Id)
            .ToListAsync();
        
        var result = new List<Guid> { primaryUserId };
        result.AddRange(aliases);
        return result;
    }

    public async Task<bool> CanBecomeAliasAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.Aliases)
            .Include(u => u.TimeProfiles)
            .FirstOrDefaultAsync(u => u.Id == userId);
        
        if (user == null) return false;
        
        // Cannot become alias if user has aliases pointing to it
        if (user.Aliases.Any()) return false;
        
        // Cannot become alias if user has active profile
        if (user.TimeProfiles.Any(p => p.IsActive)) return false;
        
        return true;
    }
}
