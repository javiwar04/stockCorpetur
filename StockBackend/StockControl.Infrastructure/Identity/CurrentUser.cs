using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Common;

namespace StockControl.Infrastructure.Identity;

/// <summary>Lee el usuario autenticado desde el JWT de la petición HTTP actual.</summary>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? Principal?.FindFirstValue("sub");

    public string? UserName => Principal?.Identity?.Name
                               ?? Principal?.FindFirstValue("unique_name");

    public bool EstaAutenticado => Principal?.Identity?.IsAuthenticated ?? false;

    public bool EsAdmin => Principal?.IsInRole(RolesApp.Admin) ?? false;
    public bool EsGerencia => Principal?.IsInRole(RolesApp.Gerencia) ?? false;

    public IReadOnlyCollection<int> HotelesPermitidos =>
        Principal?.FindAll(ClaimsApp.Hotel)
            .Select(c => int.TryParse(c.Value, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToArray() ?? [];

    /// <summary>Admin y Gerencia acceden a todo; el resto solo a sus hoteles asignados.</summary>
    public bool PuedeAccederHotel(int hotelId) =>
        EsAdmin || EsGerencia || HotelesPermitidos.Contains(hotelId);
}
