using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StockControl.Domain.Common;
using StockControl.Domain.Entities;
using StockControl.Infrastructure.Identity;

namespace StockControl.Infrastructure.Persistence;

/// <summary>
/// Aplica migraciones y siembra datos mínimos: roles, un administrador inicial,
/// las unidades de medida base y los 5 hoteles del grupo.
/// </summary>
public static class DbInitializer
{
    private static readonly string[] HotelesGrupo =
        ["Casona del Lago", "El Mesón", "Casona de la Isla", "Hotel Petén", "Villa del Lago"];

    // Nombre + abreviatura de las unidades de compra habituales.
    private static readonly (string Nombre, string Abrev)[] Unidades =
    [
        ("Libra", "lb"), ("Unidad", "u"), ("Caja", "cja"), ("Malla", "mll"),
        ("Manojo", "mjo"), ("Docena", "doc"), ("Bandeja", "bja"), ("Bolsa", "bls"),
        ("Cartón", "ctn"), ("Quintal", "qq")
    ];

    public static async Task InicializarAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var services = scope.ServiceProvider;

        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in RolesApp.Todos)
            if (!await roleManager.RoleExistsAsync(rol))
                await roleManager.CreateAsync(new IdentityRole(rol));

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var host = services.GetRequiredService<IHostEnvironment>();
        var adminEmail = config["Seed:AdminEmail"] ?? "admin@stockcontrol.local";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var adminPass = config["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(adminPass))
            {
                if (host.IsProduction())
                    throw new InvalidOperationException("Falta configurar 'Seed:AdminPassword' para crear el admin inicial.");

                adminPass = "Admin123$";
            }

            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                Nombre = "Administrador"
            };
            var res = await userManager.CreateAsync(admin, adminPass);
            if (res.Succeeded)
                await userManager.AddToRoleAsync(admin, RolesApp.Admin);
        }

        if (!await db.Unidades.AnyAsync())
            db.Unidades.AddRange(Unidades.Select(u => new UnidadMedida { Nombre = u.Nombre, Abreviatura = u.Abrev }));

        if (!await db.Hoteles.AnyAsync())
            db.Hoteles.AddRange(HotelesGrupo.Select(h => new Hotel { Nombre = h }));

        await db.SaveChangesAsync();
    }
}
