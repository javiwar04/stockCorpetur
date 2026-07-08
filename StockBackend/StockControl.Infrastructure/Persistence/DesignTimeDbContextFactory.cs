using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockControl.Infrastructure.Persistence;

/// <summary>
/// Permite a las herramientas de EF Core (dotnet ef migrations) construir el
/// contexto en tiempo de diseño sin arrancar la API. No requiere BD viva.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("STOCKCONTROL_CONN")
                   ?? "Server=localhost;Database=StockControl;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(conn)
            .Options;

        return new AppDbContext(options);
    }
}
