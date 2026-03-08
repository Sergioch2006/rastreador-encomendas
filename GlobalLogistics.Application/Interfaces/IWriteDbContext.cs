using GlobalLogistics.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlobalLogistics.Application.Interfaces;

public interface IWriteDbContext
{
    DbSet<Package> Packages { get; }
    DbSet<TrackingEvent> TrackingEvents { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
