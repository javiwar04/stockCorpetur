namespace StockControl.Application.Common.Interfaces;

/// <summary>Usuario autenticado de la petición actual. La implementación lee el JWT.</summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    bool EstaAutenticado { get; }
    bool EsAdmin { get; }
    bool EsGerencia { get; }

    /// <summary>Hoteles a los que el usuario tiene acceso. Vacío = todos (Admin/Gerencia).</summary>
    IReadOnlyCollection<int> HotelesPermitidos { get; }

    bool PuedeAccederHotel(int hotelId);
}
