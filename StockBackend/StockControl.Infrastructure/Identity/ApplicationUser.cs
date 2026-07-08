using Microsoft.AspNetCore.Identity;

namespace StockControl.Infrastructure.Identity;

/// <summary>
/// Usuario de la aplicación (extiende Identity). El scoping por hotel se modela
/// con <see cref="Hoteles"/>: un Digitador solo opera sobre los hoteles asignados;
/// Admin/Gerencia ignoran esta lista (ven todo).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; } = true;

    /// <summary>Refresh token vigente (rotado en cada uso). Null si no hay sesión activa.</summary>
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpira { get; set; }

    public ICollection<UsuarioHotel> Hoteles { get; set; } = new List<UsuarioHotel>();
}
