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
        if (user is null || user.RefreshTokenExpira < DateTime.UtcNow)
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
        if (!RolesApp.Todos.Contains(req.Rol))
            return BadRequest($"Rol inválido. Válidos: {string.Join(", ", RolesApp.Todos)}.");

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            EmailConfirmed = true,
            Nombre = req.Nombre
        };

        var res = await userManager.CreateAsync(user, req.Password);
        if (!res.Succeeded)
            return BadRequest(res.Errors.Select(e => e.Description));

        await userManager.AddToRoleAsync(user, req.Rol);

        // Solo los Digitadores se restringen por hotel.
        if (req.Rol == RolesApp.Digitador && req.Hoteles is { Count: > 0 })
        {
            foreach (var hotelId in req.Hoteles.Distinct())
                db.UsuariosHoteles.Add(new UsuarioHotel { UsuarioId = user.Id, HotelId = hotelId });
            await db.SaveChangesAsync();
        }

        return new UsuarioInfo(user.Id, user.Nombre, user.Email!, [req.Rol], req.Hoteles ?? []);
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
        await userManager.UpdateAsync(usuario);
        return NoContent();
    }

    private async Task<AuthResponse> EmitirTokensAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var hoteles = await db.UsuariosHoteles.Where(uh => uh.UsuarioId == user.Id)
            .Select(uh => uh.HotelId).ToListAsync();

        var tokens = jwt.Generar(user.Id, user.UserName!, roles, hoteles);

        user.RefreshToken = tokens.RefreshToken;
        user.RefreshTokenExpira = DateTime.UtcNow.AddDays(7);
        await userManager.UpdateAsync(user);

        return new AuthResponse(
            tokens.AccessToken, tokens.ExpiraEn, tokens.RefreshToken,
            new UsuarioInfo(user.Id, user.Nombre, user.Email!, roles.ToArray(), hoteles));
    }
}
