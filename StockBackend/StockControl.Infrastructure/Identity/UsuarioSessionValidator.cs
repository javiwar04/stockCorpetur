using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StockControl.Infrastructure.Persistence;

namespace StockControl.Infrastructure.Identity;

public class UsuarioSessionValidator(AppDbContext db)
{
    public async Task<bool> EsValidaAsync(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = principal.FindFirstValue("security_stamp");
        if (id is null || stamp is null) return false;

        return await db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == id && u.Activo && u.SecurityStamp == stamp);
    }
}
