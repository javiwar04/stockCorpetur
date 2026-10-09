using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StockControl.Api.Contracts.Auth;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Common;
using StockControl.Infrastructure.Identity;
using StockControl.Infrastructure.Persistence;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenGenerator jwt,
    AppDbContext db) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null || !user.Activo)
            return Unauthorized("Credenciales inválidas.");

        if (await userManager.IsLockedOutAsync(user))
            return Unauthorized("Cuenta bloqueada temporalmente por intentos fallidos.");

        if (!await userManager.CheckPasswordAsync(user, req.Password))
        {
            await userManager.AccessFailedAsync(user);
            return Unauthorized("Credenciales inválidas.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await EmitirTokensAsync(user);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshToken == req.RefreshToken);
        if (user is null || !user.Activo || user.RefreshTokenExpira is null || user.RefreshTokenExpira <= DateTime.UtcNow)
            return Unauthorized("Refresh token inválido o expirado.");

        return await EmitirTokensAsync(user);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UsuarioInfo>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        var hoteles = await db.UsuariosHoteles.Where(uh => uh.UsuarioId == user.Id)
            .Select(uh => uh.HotelId).ToListAsync();

        return new UsuarioInfo(user.Id, user.Nombre, user.Email!, roles.ToArray(), hoteles);
    }

    [HttpPost("usuarios")]
    [Authorize(Roles = RolesApp.Admin)]
    public async Task<ActionResult<UsuarioInfo>> CrearUsuario(CrearUsuarioRequest req)
    {
        var error = await ValidarAccesoAsync(req.Nombre, req.Rol, req.Hoteles);
        if (error is not null) return BadRequest(new { error });

        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = new ApplicationUser
        {
            UserName = req.Email.Trim(),
            Email = req.Email.Trim(),
            EmailConfirmed = true,
            Nombre = req.Nombre.Trim()
        };

        var res = await userManager.CreateAsync(user, req.Password);
        if (!res.Succeeded)
            return BadRequest(res.Errors.Select(e => e.Description));

        res = await userManager.AddToRoleAsync(user, req.Rol);
        if (!res.Succeeded) return BadRequest(res.Errors.Select(e => e.Description));

        // Los roles operativos restringidos se asignan a hoteles concretos.
        if (RolesApp.UsaRestriccionHoteles(req.Rol) && req.Hoteles is { Count: > 0 })
        {
            foreach (var hotelId in req.Hoteles.Distinct())
                db.UsuariosHoteles.Add(new UsuarioHotel { UsuarioId = user.Id, HotelId = hotelId });
            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return new UsuarioInfo(user.Id, user.Nombre, user.Email!, [req.Rol],
            RolesApp.UsaRestriccionHoteles(req.Rol) ? req.Hoteles!.Distinct().ToArray() : []);
    }

    [HttpGet("usuarios")]
    [Authorize(Roles = RolesApp.Admin)]
    public async Task<ActionResult<List<UsuarioListaDto>>> ListarUsuarios()
    {
        var usuarios = await db.Users.OrderBy(u => u.Nombre).ToListAsync();
        var roles = await (
            from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            select new { ur.UserId, r.Name }).ToListAsync();
        var hoteles = await db.UsuariosHoteles.ToListAsync();

        return usuarios.Select(u => new UsuarioListaDto(
            u.Id,
            u.Nombre,
            u.Email ?? "",
            roles.Where(r => r.UserId == u.Id).Select(r => r.Name!).ToArray(),
            hoteles.Where(h => h.UsuarioId == u.Id).Select(h => h.HotelId).ToArray(),
            u.Activo)).ToList();
    }

    [HttpPut("usuarios/{id}")]
    [Authorize(Roles = RolesApp.Admin)]
    public async Task<ActionResult<UsuarioListaDto>> ActualizarUsuario(string id, ActualizarUsuarioRequest req)
    {
        var error = await ValidarAccesoAsync(req.Nombre, req.Rol, req.Hoteles);
        if (error is not null) return BadRequest(new { error });

        var usuario = await userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();
        var actual = await userManager.GetUserAsync(User);
        if (actual?.Id == id && (!req.Activo || req.Rol != RolesApp.Admin))
            return BadRequest(new { error = "No puedes desactivar tu cuenta ni quitarte el rol Admin." });

        await using var transaction = await db.Database.BeginTransactionAsync();
        usuario.Nombre = req.Nombre.Trim();
        usuario.Email = req.Email.Trim();
        usuario.UserName = req.Email.Trim();
        usuario.Activo = req.Activo;
        var res = await userManager.UpdateAsync(usuario);
        if (!res.Succeeded) return BadRequest(res.Errors.Select(e => e.Description));

        if (!string.IsNullOrEmpty(req.Password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
            res = await userManager.ResetPasswordAsync(usuario, token, req.Password);
            if (!res.Succeeded) return BadRequest(res.Errors.Select(e => e.Description));
        }

        var roles = await userManager.GetRolesAsync(usuario);
        res = await userManager.RemoveFromRolesAsync(usuario, roles.Where(r => r != req.Rol));
        if (!res.Succeeded) return BadRequest(res.Errors.Select(e => e.Description));
        if (!roles.Contains(req.Rol))
        {
            res = await userManager.AddToRoleAsync(usuario, req.Rol);
            if (!res.Succeeded) return BadRequest(res.Errors.Select(e => e.Description));
        }

        var hoteles = RolesApp.UsaRestriccionHoteles(req.Rol) ? req.Hoteles!.Distinct().ToArray() : [];
        var asignaciones = await db.UsuariosHoteles.Where(h => h.UsuarioId == id).ToListAsync();
        db.UsuariosHoteles.RemoveRange(asignaciones.Where(h => !hoteles.Contains(h.HotelId)));
        foreach (var hotelId in hoteles.Except(asignaciones.Select(h => h.HotelId)))
            db.UsuariosHoteles.Add(new UsuarioHotel { UsuarioId = id, HotelId = hotelId });
        await db.SaveChangesAsync();

        // Los JWT emitidos antes del cambio dejan de autorizar con permisos antiguos.
        usuario.RefreshToken = null;
        usuario.RefreshTokenExpira = null;
        res = await userManager.UpdateSecurityStampAsync(usuario);
        if (!res.Succeeded) return BadRequest(res.Errors.Select(e => e.Description));
        await transaction.CommitAsync();
        return new UsuarioListaDto(id, usuario.Nombre, usuario.Email!, [req.Rol], hoteles, usuario.Activo);
    }

    /// <summary>Activa o desactiva un usuario. Un usuario inactivo no puede iniciar sesión.</summary>
    [HttpPut("usuarios/{id}/activo")]
    [Authorize(Roles = RolesApp.Admin)]
    public async Task<IActionResult> CambiarActivo(string id, ActivarUsuarioRequest req)
    {
        var actual = await userManager.GetUserAsync(User);
        if (actual?.Id == id)
            return BadRequest(new { error = "No puedes desactivar tu propia cuenta." });

        var usuario = await userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();

        usuario.Activo = req.Activo;
        if (!req.Activo)
        {
            // Corta la sesión: invalida el refresh token vigente.
            usuario.RefreshToken = null;
            usuario.RefreshTokenExpira = null;
        }
        var res = await userManager.UpdateSecurityStampAsync(usuario);
        if (!res.Succeeded) return BadRequest(res.Errors.Select(e => e.Description));
        return NoContent();
    }

    private async Task<string?> ValidarAccesoAsync(string nombre, string rol, IReadOnlyCollection<int>? hoteles)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "Ingresa el nombre del usuario.";
        if (!RolesApp.Todos.Contains(rol)) return "Selecciona un rol valido.";
        if (!RolesApp.UsaRestriccionHoteles(rol)) return null;
        if (hoteles is not { Count: > 0 }) return $"El rol {rol} necesita al menos un establecimiento asignado.";
        var ids = hoteles.Distinct().ToArray();
        if (await db.Hoteles.CountAsync(h => ids.Contains(h.Id)) != ids.Length)
            return "Uno o mas establecimientos no existen.";
        return null;
    }

    private async Task<AuthResponse> EmitirTokensAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var hoteles = await db.UsuariosHoteles.Where(uh => uh.UsuarioId == user.Id)
            .Select(uh => uh.HotelId).ToListAsync();

        var tokens = jwt.Generar(user.Id, user.UserName!, roles, hoteles, user.SecurityStamp!);

        user.RefreshToken = tokens.RefreshToken;
        user.RefreshTokenExpira = DateTime.UtcNow.AddDays(7);
        await userManager.UpdateAsync(user);

        return new AuthResponse(
            tokens.AccessToken, tokens.ExpiraEn, tokens.RefreshToken,
            new UsuarioInfo(user.Id, user.Nombre, user.Email!, roles.ToArray(), hoteles));
    }
}
