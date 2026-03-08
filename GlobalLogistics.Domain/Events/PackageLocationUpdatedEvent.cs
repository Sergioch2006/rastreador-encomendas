namespace GlobalLogistics.Domain.Events;

// Este contrato é compartilhado entre Publisher (IngestionAPI) e Consumer (Worker)
public record PackageLocationUpdatedEvent
{
    public Guid PackageId { get; init; }
    public string TrackingCode { get; init; } = string.Empty;
    public int Status { get; init; }
    public string Location { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
