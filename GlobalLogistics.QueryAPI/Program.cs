using GlobalLogistics.Application.Interfaces;
using GlobalLogistics.Application.Queries;
using GlobalLogistics.Infrastructure.Cache;
using GlobalLogistics.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;  // ← JWT Auth
using Microsoft.IdentityModel.Tokens;                   // ← Token validation
using System.Text;                                      // ← Encoding para signing key
using System.Security.Claims;                           // ← Extrair claims do usuário

var builder = WebApplication.CreateBuilder(args);

// 🪵 Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// 🗄️ DbContext (Read — AsNoTracking por padrão)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"))
           .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.AddScoped<IReadDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// 🗃️ Redis Cache
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// 🔄 MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GetPackageTrackingQuery).Assembly));

// 🏥 Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("SqlServer")!, name: "sqlserver")
    .AddRedis(builder.Configuration["Redis:ConnectionString"]!, name: "redis");

// 📚 OpenAPI
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
            
            ClockSkew = TimeSpan.FromMinutes(2)  // Tolerância para sincronização de containers
        };
        
        options.MapInboundClaims = false;             // Preserva formato original das claims
        
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
    app.MapOpenApi();
}

// ⚠️ AUTH MIDDLEWARE - ORDEM CORRETA

// ⚠️ AUTH MIDDLEWARE - ORDEM CORRETA
app.UseAuthentication();  // ← Valida o token JWT
app.UseAuthorization();   // ← Aplica políticas RequireAuthorization

// === ENDPOINTS ===

// 🏥 Health Check (PÚBLICO - sem autenticação)
app.MapHealthChecks("/health");

// 🔍 GET /api/tracking/{trackingCode} (✅ PROTEGIDO)
app.MapGet("/api/tracking/{trackingCode}", async (
    string trackingCode,
    IMediator mediator,
    HttpContext httpContext,  // ← Para extrair claims do usuário (opcional)
    CancellationToken ct) =>
{
    // 🔐 Opcional: Auditoria - log de quem consultou
    var userEmail = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
    Log.Information("🔍 Consulta de rastreamento {TrackingCode} por {UserEmail}", 
        trackingCode, userEmail);

    var result = await mediator.Send(new GetPackageTrackingQuery(trackingCode), ct);

    return result is not null
        ? Results.Ok(result)
        : Results.NotFound(new { Message = $"Pacote '{trackingCode}' não encontrado." });
})
.RequireAuthorization()  // 🔐 PROTEGE ESTE ENDPOINT
.WithName("GetPackageTracking");

app.Run();