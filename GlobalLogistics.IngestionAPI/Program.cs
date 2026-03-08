using GlobalLogistics.Application.Commands;
using GlobalLogistics.Domain.Events;
using GlobalLogistics.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// DbContext (Write)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(UpdatePackageLocationCommand).Assembly));

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:User"]!);
            h.Password(builder.Configuration["RabbitMQ:Pass"]!);
        });
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("SqlServer")!, name: "sqlserver")
    .AddRabbitMQ(rabbitConnectionString: builder.Configuration["RabbitMQ:ConnectionString"]!, name: "rabbitmq");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// === ENDPOINTS ===

// POST /api/packages — Criar novo pacote
app.MapPost("/api/packages", async (CreatePackageRequest request, AppDbContext db, CancellationToken ct) =>
{
    var package = GlobalLogistics.Domain.Entities.Package.Create(
        request.TrackingCode,
        request.SenderName,
        request.RecipientName,
        request.OriginAddress,
        request.DestinationAddress,
        request.WeightKg);

    db.Packages.Add(package);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/packages/{package.TrackingCode}", new { package.Id, package.TrackingCode });
})
.WithName("CreatePackage")
.WithOpenApi();

// POST /api/tracking — Publicar evento de rastreamento
app.MapPost("/api/tracking", async (
    TrackingUpdateRequest request,
    IPublishEndpoint publishEndpoint,
    IMediator mediator,
    CancellationToken ct) =>
{
    var correlationId = await mediator.Send(
        new UpdatePackageLocationCommand(
            request.TrackingCode,
            request.Status,
            request.Location,
            request.Latitude,
            request.Longitude,
            request.Description),
        ct);

    // Publicar no RabbitMQ
    await publishEndpoint.Publish(new PackageLocationUpdatedEvent
    {
        PackageId = Guid.Empty, // Worker irá resolver pelo TrackingCode
        TrackingCode = request.TrackingCode,
        Status = request.Status,
        Location = request.Location,
        Latitude = request.Latitude,
        Longitude = request.Longitude,
        Description = request.Description
    }, ct);

    return Results.Accepted($"/api/tracking/{correlationId}", new { CorrelationId = correlationId });
})
.WithName("UpdateTracking")
.WithOpenApi();

// Health Check endpoint
app.MapHealthChecks("/health");

// Garantir que o DB existe ao iniciar
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

// Request records
record CreatePackageRequest(
    string TrackingCode,
    string SenderName,
    string RecipientName,
    string OriginAddress,
    string DestinationAddress,
    double WeightKg);

record TrackingUpdateRequest(
    string TrackingCode,
    int Status,
    string Location,
    double Latitude,
    double Longitude,
    string Description);
