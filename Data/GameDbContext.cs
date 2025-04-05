using Microsoft.EntityFrameworkCore;
using AiMarketplaceApi.Models;

namespace AiMarketplaceApi.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    public DbSet<Level> Levels { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserLevel> UserLevels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Update ChatMessage relationship to point to UserLevel instead of Level
        modelBuilder.Entity<ChatMessage>()
            .HasOne(c => c.UserLevel)
            .WithMany(ul => ul.ChatMessages)
            .HasForeignKey(c => c.UserLevelId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserLevel relationships
        modelBuilder.Entity<UserLevel>()
            .HasOne(ul => ul.User)
            .WithMany(u => u.UserLevels)
            .HasForeignKey(ul => ul.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserLevel>()
            .HasOne(ul => ul.Level)
            .WithMany(l => l.UserLevels)
            .HasForeignKey(ul => ul.LevelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure decimal precision for prices
        modelBuilder.Entity<Level>()
            .Property(l => l.InitialPrice)
            .HasPrecision(18, 2);  // 18 digits total, 2 decimal places
        
        modelBuilder.Entity<Level>()
            .Property(l => l.TargetPrice)
            .HasPrecision(18, 2);

        // Configure decimal precision for LastOfferedPrice in UserLevel
        modelBuilder.Entity<UserLevel>()
            .Property(ul => ul.LastOfferedPrice)
            .HasPrecision(18, 2);
    }
} 