using GlobalLogistics.Domain.Entities;
using GlobalLogistics.Domain.Enums;
using GlobalLogistics.Domain.Events;
using GlobalLogistics.Infrastructure.Cache;
using GlobalLogistics.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace GlobalLogistics.Worker;

public class PackageLocationUpdatedConsumer : IConsumer<PackageLocationUpdatedEvent>
{
    private readonly AppDbContext _db;
    private readonly RedisCacheService _cache;
    private readonly ILogger<PackageLocationUpdatedConsumer> _logger;

    public PackageLocationUpdatedConsumer(
        AppDbContext db,
        RedisCacheService cache,
        ILogger<PackageLocationUpdatedConsumer> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PackageLocationUpdatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Processando evento para pacote {TrackingCode}", msg.TrackingCode);

        // Buscar pacote pelo TrackingCode
        var package = await _db.Packages
            .FirstOrDefaultAsync(p => p.TrackingCode == msg.TrackingCode, context.CancellationToken);

        if (package is null)
        {
            _logger.LogWarning("Pacote {TrackingCode} não encontrado", msg.TrackingCode);
            return;
        }

        var status = (PackageStatus)msg.Status;

        // Atualizar status do pacote
        package.UpdateStatus(status);

        // Criar evento de rastreamento
        var trackingEvent = TrackingEvent.Create(
            package.Id,
            status,
            msg.Location,
            msg.Latitude,
            msg.Longitude,
            msg.Description);

        _db.TrackingEvents.Add(trackingEvent);
        await _db.SaveChangesAsync(context.CancellationToken);

        // Invalidar cache Redis para forçar refresh na próxima consulta
        await _cache.RemoveAsync($"tracking:{msg.TrackingCode}", context.CancellationToken);

        _logger.LogInformation(
            "Pacote {TrackingCode} atualizado para status {Status} em {Location}",
            msg.TrackingCode, status, msg.Location);
    }
}
