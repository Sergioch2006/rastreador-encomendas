using GlobalLogistics.Domain.Common;
using GlobalLogistics.Domain.Enums;

namespace GlobalLogistics.Domain.Entities;

public class Package : BaseEntity
{
    public string TrackingCode { get; private set; } = string.Empty;
    public string SenderName { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string OriginAddress { get; private set; } = string.Empty;
    public string DestinationAddress { get; private set; } = string.Empty;
    public PackageStatus Status { get; private set; } = PackageStatus.Created;
    public double WeightKg { get; private set; }

    // Navegação EF Core
    public ICollection<TrackingEvent> TrackingHistory { get; private set; } = new List<TrackingEvent>();

    // Construtor privado para EF Core
    private Package() { }

    public static Package Create(
        string trackingCode,
        string senderName,
        string recipientName,
        string originAddress,
        string destinationAddress,
        double weightKg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);

        return new Package
        {
            TrackingCode = trackingCode,
            SenderName = senderName,
            RecipientName = recipientName,
            OriginAddress = originAddress,
            DestinationAddress = destinationAddress,
            WeightKg = weightKg,
            Status = PackageStatus.Created
        };
    }

    public void UpdateStatus(PackageStatus newStatus)
    {
        Status = newStatus;
        SetUpdated();
    }
}
