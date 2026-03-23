using GlobalLogistics.Application.Commands;
using GlobalLogistics.Domain.Events;
using GlobalLogistics.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims; // ← Para extrair claims do usuário
using Serilog.Core;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// 🪵 Serilog
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("service", "ingestion-api")
        .Enrich.WithProperty("environment", context.HostingEnvironment.EnvironmentName)
        .Enrich.With(new LevelLabelEnricher()));

// 🗄️ DbContext (Write)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

// 🔄 MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(UpdatePackageLocationCommand).Assembly));

// Remove handlers from Queries namespace para evitar erros de dependência
var queryHandlers = builder.Services
    .Where(d => d.ImplementationType?.Namespace?.StartsWith("GlobalLogistics.Application.Queries") == true)
    .ToList();

foreach (var handler in queryHandlers)
{
    builder.Services.Remove(handler);
}

// 🐰 MassTransit + RabbitMQ
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

// 🏥 Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("SqlServer")!, name: "sqlserver")
    .AddRabbitMQ(rabbitConnectionString: builder.Configuration["RabbitMQ:ConnectionString"]!, name: "rabbitmq");

// 📚 OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 🔐 AUTENTICAÇÃO JWT (✅ REGISTRADO ANTES DO builder.Build())
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            
            ValidIssuer = builder.Configuration["Auth:JwtIssuer"],
            ValidAudience = builder.Configuration["Auth:JwtAudience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Auth:JwtSigningKey"]!)),
            
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        
        options.MapInboundClaims = false;
        
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("❌ JWT auth failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
                Log.Debug("✅ Token validado para: {Email}", email);
                return Task.CompletedTask;
            }
        };
    });

// 🔐 Autorização com política
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAuthenticated", policy => policy.RequireAuthenticatedUser());

// ✅ BUILD DO APP (APENAS UMA VEZ!)
var app = builder.Build();

// 🔄 Middleware pipeline (ORDEM É CRÍTICA!)
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // ← Você está usando AddOpenApi(), não Swagger
}

// ⚠️ AUTH MIDDLEWARE - ORDEM CORRETA

// ⚠️ AUTH MIDDLEWARE - ORDEM CORRETA
app.UseAuthentication();  // ← Valida o token JWT
app.UseAuthorization();   // ← Aplica políticas [Authorize]/RequireAuthorization

// === ENDPOINTS ===

// 🏥 Health Check (PÚBLICO - sem autenticação)
app.MapHealthChecks("/health");

// 📦 POST /api/packages — Criar novo pacote (✅ PROTEGIDO)
app.MapPost("/api/packages", async (
    CreatePackageRequest request, 
    AppDbContext db, 
    HttpContext httpContext,  // ← Para extrair claims do usuário
    CancellationToken ct) =>
{
    // 🔐 Opcional: Auditoria - extrair usuário autenticado
    var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var userEmail = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
    
    Log.Information("📦 Criando pacote {TrackingCode} por {UserEmail}", 
        request.TrackingCode, userEmail);

    var package = GlobalLogistics.Domain.Entities.Package.Create(
        request.TrackingCode,
        request.SenderName,
        request.RecipientName,
        request.OriginAddress,
        request.DestinationAddress,
        request.WeightKg);

    // 🔐 Opcional: Salvar informações de auditoria
    // package.CreatedBy = userId;
    // package.CreatedByEmail = userEmail;

    db.Packages.Add(package);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/packages/{package.TrackingCode}", new { package.Id, package.TrackingCode });
})
.RequireAuthorization()  // 🔐 PROTEGE ESTE ENDPOINT
.WithName("CreatePackage");

// 📍 POST /api/tracking — Publicar evento de rastreamento (✅ PROTEGIDO)
app.MapPost("/api/tracking", async (
    TrackingUpdateRequest request,
    IPublishEndpoint publishEndpoint,
    IMediator mediator,
    HttpContext httpContext,  // ← Para extrair claims
    CancellationToken ct) =>
{
    // 🔐 Auditoria: log com usuário autenticado
    var userEmail = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
    Log.Information("📍 Evento de rastreamento para {TrackingCode} por {UserEmail}", 
        request.TrackingCode, userEmail);

    var correlationId = await mediator.Send(
        new UpdatePackageLocationCommand(
            request.TrackingCode,
            request.Status,
            request.Location,
            request.Latitude,
            request.Longitude,
            request.Description),
        ct);

    await publishEndpoint.Publish(new PackageLocationUpdatedEvent
    {
        PackageId = Guid.Empty,
        TrackingCode = request.TrackingCode,
        Status = request.Status,
        Location = request.Location,
        Latitude = request.Latitude,
        Longitude = request.Longitude,
        Description = request.Description,
        // 🔐 Opcional: incluir usuário no evento para auditoria no Worker
        // TriggeredBy = userEmail
    }, ct);

    return Results.Accepted($"/api/tracking/{correlationId}", new { CorrelationId = correlationId });
})
.RequireAuthorization()  // 🔐 PROTEGE ESTE ENDPOINT
.WithName("UpdateTracking");

// 🔄 Garantir que o DB existe ao iniciar (apenas em desenvolvimento ou com flag)
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}

app.Run();

// === REQUEST RECORDS ===
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

internal sealed class LevelLabelEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("level", logEvent.Level.ToString()));
    }
}
