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
        
        // Configurações adicionais se necessário
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users", "auth");
            entity.Property(u => u.FullName).HasMaxLength(100);
        });
    }
}
