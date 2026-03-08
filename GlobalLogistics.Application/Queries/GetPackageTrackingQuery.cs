using GlobalLogistics.Application.DTOs;
using GlobalLogistics.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GlobalLogistics.Application.Queries;

public record GetPackageTrackingQuery(string TrackingCode) : IRequest<PackageTrackingDto?>;

public class GetPackageTrackingHandler : IRequestHandler<GetPackageTrackingQuery, PackageTrackingDto?>
{
    private readonly IReadDbContext _db;
    private readonly ICacheService _cache;

    public GetPackageTrackingHandler(IReadDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<PackageTrackingDto?> Handle(GetPackageTrackingQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"tracking:{request.TrackingCode}";

        // Tentar cache primeiro (Redis)
        var cached = await _cache.GetAsync<PackageTrackingDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // Fallback: banco de dados
        var package = await _db.Packages
            .Include(p => p.TrackingHistory.OrderByDescending(e => e.OccurredAt))
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TrackingCode == request.TrackingCode, cancellationToken);

        if (package is null)
            return null;

        var dto = new PackageTrackingDto(
            package.Id,
            package.TrackingCode,
            package.SenderName,
            package.RecipientName,
            package.OriginAddress,
            package.DestinationAddress,
            package.Status.ToString(),
            package.WeightKg,
            package.TrackingHistory.Select(e => new TrackingEventDto(
                e.Status.ToString(),
                e.Location,
                e.Latitude,
                e.Longitude,
                e.Description,
                e.OccurredAt
            ))
        );

        // Salvar no cache por 5 minutos
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);

        return dto;
    }
}
