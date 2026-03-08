using GlobalLogistics.Application.Interfaces;
using GlobalLogistics.Application.Queries;
using GlobalLogistics.Infrastructure.Cache;
using GlobalLogistics.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// DbContext (Read — AsNoTracking por padrão)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"))
           .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.AddScoped<IReadDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GetPackageTrackingQuery).Assembly));

// Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("SqlServer")!, name: "sqlserver")
    .AddRedis(builder.Configuration["Redis:ConnectionString"]!, name: "redis");

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

// GET /api/tracking/{trackingCode}
app.MapGet("/api/tracking/{trackingCode}", async (
    string trackingCode,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(new GetPackageTrackingQuery(trackingCode), ct);

    return result is not null
        ? Results.Ok(result)
        : Results.NotFound(new { Message = $"Pacote '{trackingCode}' não encontrado." });
})
.WithName("GetPackageTracking")
.WithOpenApi();

// Health Check
app.MapHealthChecks("/health");

app.Run();
