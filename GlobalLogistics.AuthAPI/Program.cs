using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using GlobalLogistics.AuthAPI.Data;
using GlobalLogistics.AuthAPI.Models;
using GlobalLogistics.AuthAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// 🔐 Configuração do Data Protection para evitar avisos no Docker
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"/app/keys"));

// 🔐 Configuração das chaves JWT (use variáveis de ambiente em produção!)
var jwtSettings = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSettings["SigningKey"] 
    ?? throw new InvalidOperationException("Jwt:SigningKey não configurada");

// 🗄️ Configurar DbContext com SQL Server
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AuthDb"),
        sql => sql.MigrationsAssembly(typeof(AuthDbContext).Assembly.FullName)));

// 👤 Configurar ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Políticas de senha
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    
    // Políticas de usuário
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AuthDbContext>()
.AddDefaultTokenProviders();

// 🔐 Configurar Authentication com JWT Bearer
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(signingKey)),
        
        // Tolerância para diferença de relógio entre containers
        ClockSkew = TimeSpan.FromMinutes(2)
    };
    
    // Não mapear claims automaticamente para preservar formato original
    options.MapInboundClaims = false;
    
    // Logging de eventos para debugging
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"❌ Auth failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"✅ Token validated for: {context.Principal?.Identity?.Name}");
            return Task.CompletedTask;
        }
    };
});

// 🎯 Configurar Authorization com políticas
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireActiveUser", policy =>
        policy.Requirements.Add(new ActiveUserRequirement()));

// 📚 Configurar OpenAPI com suporte a JWT
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 🎮 Controllers
builder.Services.AddControllers();

builder.Services.AddScoped<ITokenService, TokenService>();

var app = builder.Build();

// 🔄 Middleware pipeline (ORDEM É CRÍTICA!)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication(); // ⚠️ DEVE vir antes de UseAuthorization
app.UseAuthorization();

// 🔄 MIGRATION ANTES DOS ENDPOINTS
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    
    // Cria o banco LogisticsAuth se não existir + aplica migrations
    await dbContext.Database.MigrateAsync();
}

app.MapControllers();

// 🏥 Health check
app.MapHealthChecks("/health", new AddHealthChecksOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

// 📋 Requirement para usuário ativo
public class ActiveUserRequirement : IAuthorizationRequirement { }

public class ActiveUserRequirementHandler : AuthorizationHandler<ActiveUserRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        ActiveUserRequirement requirement)
    {
        // Implementação simplificada - em produção, valide no banco
        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
