using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Models;

namespace ParentalControl.WebService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<User> Users => Set<User>();
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<TimeProfile> TimeProfiles => Set<TimeProfile>();
    public DbSet<AllowedHours> AllowedHours => Set<AllowedHours>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<TimeUsage> TimeUsage => Set<TimeUsage>();
    public DbSet<TimeAdjustment> TimeAdjustments => Set<TimeAdjustment>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(64);
            entity.Property(e => e.AccountType).HasConversion<string>();
        });
        
        modelBuilder.Entity<Computer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Hostname).IsUnique();
            entity.HasIndex(e => e.MachineId).IsUnique();
        });
        
        modelBuilder.Entity<TimeProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User).WithMany(u => u.TimeProfiles).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.Name }).IsUnique();
        });
        
        modelBuilder.Entity<AllowedHours>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Profile).WithMany(p => p.AllowedHours).HasForeignKey(e => e.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User).WithMany(u => u.Sessions).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Computer).WithMany(c => c.Sessions).HasForeignKey(e => e.ComputerId).OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<TimeUsage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ComputerId, e.UsageDate }).IsUnique();
        });
        
        modelBuilder.Entity<TimeAdjustment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
