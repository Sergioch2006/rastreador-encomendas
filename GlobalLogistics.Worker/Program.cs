using GlobalLogistics.Domain.Events;
using GlobalLogistics.Infrastructure.Cache;
using GlobalLogistics.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;
using GlobalLogistics.Worker;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));
builder.Services.AddScoped<RedisCacheService>();

// MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PackageLocationUpdatedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:User"]!);
            h.Password(builder.Configuration["RabbitMQ:Pass"]!);
        });

        cfg.ReceiveEndpoint("package-location-updated", e =>
        {
            e.ConfigureConsumer<PackageLocationUpdatedConsumer>(ctx);
            e.UseMessageRetry(r => r.Intervals(100, 500, 1000)); // Retry automático
        });
    });
});

var host = builder.Build();
host.Run();
