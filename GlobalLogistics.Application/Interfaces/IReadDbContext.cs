using GlobalLogistics.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlobalLogistics.Application.Interfaces;

public interface IReadDbContext
{
    DbSet<Package> Packages { get; }
    DbSet<TrackingEvent> TrackingEvents { get; }
}
