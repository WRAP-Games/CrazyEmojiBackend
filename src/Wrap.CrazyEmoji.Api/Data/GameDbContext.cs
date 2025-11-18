using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Data.Entities;

namespace Wrap.CrazyEmoji.Api.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<GameEntity> Games { get; set; }
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<GameRoundEntity> GameRounds { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GameEntity>(entity =>
        {
            entity.HasKey(e => e.RoomCode);

            entity.HasOne(e => e.Host)
                  .WithMany()
                  .HasForeignKey(e => e.HostId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Users)
                  .WithOne(u => u.Game)
                  .HasForeignKey(u => u.RoomCode)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Rounds)
                  .WithOne(r => r.Game)
                  .HasForeignKey(r => r.RoomCode)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();

            entity.HasMany(e => e.CommandedRounds)
                  .WithOne(r => r.Commander)
                  .HasForeignKey(r => r.CommanderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameRoundEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            // entity.Property(e => e.Id).UseSerialColumn(); // Removed deprecated method. Default conventions suffice.
        });
    }
}