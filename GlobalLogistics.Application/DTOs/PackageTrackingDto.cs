namespace GlobalLogistics.Application.DTOs;

public record PackageTrackingDto(
    Guid Id,
    string TrackingCode,
    string SenderName,
    string RecipientName,
    string OriginAddress,
    string DestinationAddress,
    string CurrentStatus,
    double WeightKg,
    IEnumerable<TrackingEventDto> History
);

public record TrackingEventDto(
    string Status,
    string Location,
    double Latitude,
    double Longitude,
    string Description,
    DateTime OccurredAt
);
