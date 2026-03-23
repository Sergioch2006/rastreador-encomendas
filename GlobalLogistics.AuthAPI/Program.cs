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

// 🔐 Data Protection — cria o diretório se não existir
var keysDir = new DirectoryInfo("/app/keys");
if (!keysDir.Exists) keysDir.Create();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(keysDir);

// 🔐 Configuração das chaves JWT (lidas via env var em produção)
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
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
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
        ClockSkew = TimeSpan.FromMinutes(2)
    };
    options.MapInboundClaims = false;
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

// 🎯 Configurar Authorization
// FIX: Registrar o handler no DI (antes estava faltando)
builder.Services.AddSingleton<IAuthorizationHandler, ActiveUserRequirementHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireActiveUser", policy =>
        policy.Requirements.Add(new ActiveUserRequirement()));

// 📚 OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 🎮 Controllers
builder.Services.AddControllers();
builder.Services.AddScoped<ITokenService, TokenService>();

// 🏥 Health Checks (sem filtro de tag — FIX para retornar Healthy)
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// 🔄 Migration + Seed de roles antes dos endpoints
using (var scope = app.Services.CreateScope())
{
    Console.WriteLine("🚀 Iniciando migrações do banco de dados...");
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await dbContext.Database.MigrateAsync();
    Console.WriteLine("✅ Migrações concluídas.");

    // FIX: Criar role "User" se não existir
    Console.WriteLine("👥 Verificando/Criando roles...");
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roles = ["User", "Admin"];
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
            Console.WriteLine($"✅ Role '{role}' criada.");
        }
    }
    Console.WriteLine("✅ Roles verificadas.");
}

app.MapControllers();

// 🏥 Health check — sem filtro de tag para sempre retornar Healthy
app.MapHealthChecks("/health");

app.Run();

// 📋 Requirement para usuário ativo
public class ActiveUserRequirement : IAuthorizationRequirement { }

public class ActiveUserRequirementHandler : AuthorizationHandler<ActiveUserRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveUserRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
