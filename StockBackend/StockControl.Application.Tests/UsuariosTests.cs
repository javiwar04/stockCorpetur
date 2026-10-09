using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockControl.Api.Contracts.Auth;
using StockControl.Api.Controllers;
using StockControl.Domain.Common;
using StockControl.Infrastructure.Identity;
using StockControl.Infrastructure.Persistence;

namespace StockControl.Application.Tests;

public class UsuariosTests
{
    [Fact]
    public async Task Editar_ReemplazaRolYHoteles_EInvalidaSesion()
    {
        await using var entorno = await Entorno.Crear();
        var usuario = entorno.Usuario;
        var stampAnterior = usuario.SecurityStamp!;
        var resultado = await entorno.Controller.ActualizarUsuario(usuario.Id,
            new("Nombre editado", "nuevo@example.com", RolesApp.SoloLectura, [2, 2], true));

        Assert.NotNull(resultado.Value);
        Assert.Equal("Nombre editado", resultado.Value.Nombre);
        Assert.Equal("nuevo@example.com", usuario.UserName);
        Assert.Equal([RolesApp.SoloLectura], await entorno.Manager.GetRolesAsync(usuario));
        var hoteles = await entorno.Db.UsuariosHoteles.Where(h => h.UsuarioId == usuario.Id).Select(h => h.HotelId).ToArrayAsync();
        Assert.Equal([2], hoteles);
        Assert.Null(usuario.RefreshToken);
        Assert.NotEqual(stampAnterior, usuario.SecurityStamp);
        Assert.False(await new UsuarioSessionValidator(entorno.Db).EsValidaAsync(Principal(usuario.Id, stampAnterior)));
        Assert.True(await new UsuarioSessionValidator(entorno.Db).EsValidaAsync(Principal(usuario.Id, usuario.SecurityStamp!)));
    }

    [Fact]
    public async Task Editar_AdminEliminaRestriccionesDeHoteles()
    {
        await using var entorno = await Entorno.Crear();
        var resultado = await entorno.Controller.ActualizarUsuario(entorno.Usuario.Id,
            new("Admin nuevo", "operador@example.com", RolesApp.Admin, [1], true));
        Assert.Empty(resultado.Value!.Hoteles);
        Assert.Empty(await entorno.Db.UsuariosHoteles.Where(h => h.UsuarioId == entorno.Usuario.Id).ToListAsync());
    }

    [Theory]
    [InlineData(RolesApp.Digitador, 0)]
    [InlineData(RolesApp.SoloLectura, 99)]
    [InlineData("Inventado", 1)]
    public async Task Editar_RechazaAccesoInvalidoSinModificar(string rol, int hotel)
    {
        await using var entorno = await Entorno.Crear();
        var resultado = await entorno.Controller.ActualizarUsuario(entorno.Usuario.Id,
            new("No guardar", "nuevo@example.com", rol, hotel == 0 ? [] : [hotel], true));
        Assert.IsType<BadRequestObjectResult>(resultado.Result);
        Assert.Equal("Operador", entorno.Usuario.Nombre);
        Assert.Equal([RolesApp.Digitador], await entorno.Manager.GetRolesAsync(entorno.Usuario));
    }

    [Theory]
    [InlineData(RolesApp.Admin, false)]
    [InlineData(RolesApp.Gerencia, true)]
    public async Task Editar_ImpideQuitarMiAccesoAdministrador(string rol, bool activo)
    {
        await using var entorno = await Entorno.Crear();
        var resultado = await entorno.Controller.ActualizarUsuario(entorno.Admin.Id,
            new("Admin", "admin@example.com", rol, [], activo));
        Assert.IsType<BadRequestObjectResult>(resultado.Result);
        Assert.True(entorno.Admin.Activo);
        Assert.True(await entorno.Manager.IsInRoleAsync(entorno.Admin, RolesApp.Admin));
    }

    [Fact]
    public async Task Editar_CambiaContrasenaSinNecesitarLaAnterior()
    {
        await using var entorno = await Entorno.Crear();
        var resultado = await entorno.Controller.ActualizarUsuario(entorno.Usuario.Id,
            new("Operador", "operador@example.com", RolesApp.Digitador, [1], true, "NuevaClave123!"));
        Assert.NotNull(resultado.Value);
        Assert.True(await entorno.Manager.CheckPasswordAsync(entorno.Usuario, "NuevaClave123!"));
        Assert.False(await entorno.Manager.CheckPasswordAsync(entorno.Usuario, "Inicial123!"));
    }

    [Fact]
    public async Task Desactivar_RechazaAccessYRefreshTokens()
    {
        await using var entorno = await Entorno.Crear();
        var stamp = entorno.Usuario.SecurityStamp!;
        var refresh = entorno.Usuario.RefreshToken!;
        await entorno.Controller.CambiarActivo(entorno.Usuario.Id, new(false));
        Assert.False(await new UsuarioSessionValidator(entorno.Db).EsValidaAsync(Principal(entorno.Usuario.Id, stamp)));
        Assert.IsType<UnauthorizedObjectResult>((await entorno.Controller.Refresh(new(refresh))).Result);
    }

    [Fact]
    public async Task Refresh_NoRenuevaUsuarioInactivoAunqueTengaToken()
    {
        await using var entorno = await Entorno.Crear();
        entorno.Usuario.Activo = false;
        await entorno.Db.SaveChangesAsync();
        Assert.IsType<UnauthorizedObjectResult>((await entorno.Controller.Refresh(new(entorno.Usuario.RefreshToken!))).Result);
    }

    private static ClaimsPrincipal Principal(string id, string stamp) =>
        new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, id), new("security_stamp", stamp)], "Test"));

    private sealed class Entorno(ServiceProvider services, AsyncServiceScope scope) : IAsyncDisposable
    {
        public AppDbContext Db { get; private set; } = null!;
        public UserManager<ApplicationUser> Manager { get; private set; } = null!;
        public ApplicationUser Usuario { get; private set; } = null!;
        public ApplicationUser Admin { get; private set; } = null!;
        public AuthController Controller { get; private set; } = null!;

        public static async Task<Entorno> Crear()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddIdentityCore<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
                .AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var e = new Entorno(provider, scope)
            {
                Db = scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                Manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            };
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var rol in RolesApp.Todos) Assert.True((await roles.CreateAsync(new(rol))).Succeeded);
            e.Db.Hoteles.AddRange(new Domain.Entities.Hotel { Id = 1, Nombre = "Uno" }, new Domain.Entities.Hotel { Id = 2, Nombre = "Dos" });
            e.Admin = new ApplicationUser { Nombre = "Admin", Email = "admin@example.com", UserName = "admin@example.com" };
            e.Usuario = new ApplicationUser { Nombre = "Operador", Email = "operador@example.com", UserName = "operador@example.com", RefreshToken = "refresh-test", RefreshTokenExpira = DateTime.UtcNow.AddDays(1) };
            Assert.True((await e.Manager.CreateAsync(e.Admin, "Inicial123!")).Succeeded);
            Assert.True((await e.Manager.CreateAsync(e.Usuario, "Inicial123!")).Succeeded);
            Assert.True((await e.Manager.AddToRoleAsync(e.Admin, RolesApp.Admin)).Succeeded);
            Assert.True((await e.Manager.AddToRoleAsync(e.Usuario, RolesApp.Digitador)).Succeeded);
            e.Db.UsuariosHoteles.Add(new UsuarioHotel { UsuarioId = e.Usuario.Id, HotelId = 1 });
            await e.Db.SaveChangesAsync();
            e.Controller = new AuthController(e.Manager,
                new JwtTokenGenerator(Options.Create(new JwtSettings { Key = new string('x', 32), Issuer = "test", Audience = "test" })), e.Db)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(e.Admin.Id, e.Admin.SecurityStamp!) } }
            };
            return e;
        }

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await services.DisposeAsync();
        }
    }
}
