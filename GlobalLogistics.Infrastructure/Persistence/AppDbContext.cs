using GlobalLogistics.Application.Interfaces;
using GlobalLogistics.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlobalLogistics.Infrastructure.Persistence;

public class AppDbContext : DbContext, IWriteDbContext, IReadDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Package> Packages => Set<Package>();
    public DbSet<TrackingEvent> TrackingEvents => Set<TrackingEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Package
        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.TrackingCode).IsRequired().HasMaxLength(50);
            entity.HasIndex(p => p.TrackingCode).IsUnique();
            entity.Property(p => p.SenderName).IsRequired().HasMaxLength(200);
            entity.Property(p => p.RecipientName).IsRequired().HasMaxLength(200);
            entity.Property(p => p.OriginAddress).IsRequired().HasMaxLength(500);
            entity.Property(p => p.DestinationAddress).IsRequired().HasMaxLength(500);
            entity.Property(p => p.Status).HasConversion<int>();
        });

        // TrackingEvent
        modelBuilder.Entity<TrackingEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Location).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasOne(e => e.Package)
                  .WithMany(p => p.TrackingHistory)
                  .HasForeignKey(e => e.PackageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
