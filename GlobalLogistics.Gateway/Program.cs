using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var ingestionHealthUrl = builder.Configuration["DownstreamServices:IngestionHealthUrl"]
    ?? throw new InvalidOperationException("DownstreamServices:IngestionHealthUrl não configurado.");
var queryHealthUrl = builder.Configuration["DownstreamServices:QueryHealthUrl"]
    ?? throw new InvalidOperationException("DownstreamServices:QueryHealthUrl não configurado.");
var authHealthUrl = builder.Configuration["DownstreamServices:AuthHealthUrl"]
    ?? throw new InvalidOperationException("DownstreamServices:AuthHealthUrl não configurado.");

builder.Services.AddHttpClient();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("PerIpPolicy", httpContext =>
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        var partitionKey = remoteIp?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services
    .AddHealthChecks()
    .AddTypeActivatedCheck<DownstreamHealthCheck>(
        "ingestion-api",
        failureStatus: HealthStatus.Unhealthy,
        args: [ingestionHealthUrl])
    .AddTypeActivatedCheck<DownstreamHealthCheck>(
        "query-api",
        failureStatus: HealthStatus.Unhealthy,
        args: [queryHealthUrl])
    .AddTypeActivatedCheck<DownstreamHealthCheck>(
        "auth-api",
        failureStatus: HealthStatus.Unhealthy,
        args: [authHealthUrl]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownIPNetworks = { },
    KnownProxies = { }
});

app.UseRateLimiter();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration,
            checks = report.Entries.Select(entry => new
            {
                service = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});

app.MapReverseProxy()
    .RequireRateLimiting("PerIpPolicy");

app.Run();

internal sealed class DownstreamHealthCheck(IHttpClientFactory httpClientFactory, string targetUrl) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(nameof(DownstreamHealthCheck));
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            using var response = await client.GetAsync(targetUrl, cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"Endpoint OK: {targetUrl}")
                : HealthCheckResult.Unhealthy($"Endpoint retornou {(int)response.StatusCode} ({response.StatusCode})");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy($"Falha ao acessar {targetUrl}", ex);
        }
    }
}
