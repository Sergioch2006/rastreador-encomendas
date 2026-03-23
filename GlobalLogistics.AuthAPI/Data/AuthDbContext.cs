using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GlobalLogistics.AuthAPI.Models;

namespace GlobalLogistics.AuthAPI.Data;

public class AuthDbContext : IdentityDbContext<ApplicationUser>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // FIX: Mapear TODAS as tabelas do Identity para o schema "auth"
        builder.Entity<ApplicationUser>(e => { e.ToTable("Users", "auth"); e.Property(u => u.FullName).HasMaxLength(100); });
        builder.Entity<IdentityRole>(e => e.ToTable("Roles", "auth"));
        builder.Entity<IdentityUserRole<string>>(e => e.ToTable("UserRoles", "auth"));
        builder.Entity<IdentityUserClaim<string>>(e => e.ToTable("UserClaims", "auth"));
        builder.Entity<IdentityUserLogin<string>>(e => e.ToTable("UserLogins", "auth"));
        builder.Entity<IdentityRoleClaim<string>>(e => e.ToTable("RoleClaims", "auth"));
        builder.Entity<IdentityUserToken<string>>(e => e.ToTable("UserTokens", "auth"));
    }
}
