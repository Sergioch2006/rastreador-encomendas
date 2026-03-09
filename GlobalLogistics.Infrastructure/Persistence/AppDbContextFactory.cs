using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GlobalLogistics.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        
        // This connection string is strictly for design-time operations (dotnet ef migrations)
        optionsBuilder.UseSqlServer("Server=sqlserver,1433;Database=LogisticsWrite;User Id=sa;Password=Encomendas@2026!;TrustServerCertificate=true;");

        return new AppDbContext(optionsBuilder.Options);
    }
}
