using GlobalLogistics.Domain.Entities;
using GlobalLogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using FluentAssertions;

namespace GlobalLogistics.Tests.Integration;

public class PackageTrackingIntegrationTest : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        await _redisContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_sqlContainer.GetConnectionString())
            .Options;

        _db = new AppDbContext(options);
        await _db.Database.MigrateAsync();
    }

    [Fact]
    public async Task Should_Create_Package_And_Add_Tracking_Event()
    {
        // Arrange
        var package = Package.Create(
            trackingCode: "BR123456789",
            senderName: "Empresa ABC",
            recipientName: "João Silva",
            originAddress: "São Paulo, SP",
            destinationAddress: "Rio de Janeiro, RJ",
            weightKg: 2.5);

        // Act
        _db.Packages.Add(package);
        await _db.SaveChangesAsync();

        var saved = await _db.Packages
            .FirstOrDefaultAsync(p => p.TrackingCode == "BR123456789");

        // Assert
        saved.Should().NotBeNull();
        saved!.TrackingCode.Should().Be("BR123456789");
        saved.SenderName.Should().Be("Empresa ABC");
    }

    [Fact]
    public async Task Should_Update_Package_Status()
    {
        // Arrange
        var package = Package.Create("BR999", "Sender", "Recipient", "Origin", "Dest", 1.0);
        _db.Packages.Add(package);
        await _db.SaveChangesAsync();

        // Act
        package.UpdateStatus(GlobalLogistics.Domain.Enums.PackageStatus.InTransit);
        await _db.SaveChangesAsync();

        var updated = await _db.Packages.FindAsync(package.Id);

        // Assert
        updated!.Status.Should().Be(GlobalLogistics.Domain.Enums.PackageStatus.InTransit);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _sqlContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}
