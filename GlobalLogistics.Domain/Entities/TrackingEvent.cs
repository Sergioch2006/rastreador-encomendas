using GlobalLogistics.Domain.Common;
using GlobalLogistics.Domain.Enums;

namespace GlobalLogistics.Domain.Entities;

public class TrackingEvent : BaseEntity
{
    public Guid PackageId { get; private set; }
    public Package Package { get; private set; } = null!;
    public PackageStatus Status { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    private TrackingEvent() { }

    public static TrackingEvent Create(
        Guid packageId,
        PackageStatus status,
        string location,
        double latitude,
        double longitude,
        string description)
    {
        return new TrackingEvent
        {
            PackageId = packageId,
            Status = status,
            Location = location,
            Latitude = latitude,
            Longitude = longitude,
            Description = description,
            OccurredAt = DateTime.UtcNow
        };
    }
}
