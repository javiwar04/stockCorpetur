using StockControl.Domain.Entities;

namespace StockControl.Infrastructure.Identity;

/// <summary>Asignación de un usuario (Digitador) a un hotel para el scoping de acceso.</summary>
public class UsuarioHotel
{
    public string UsuarioId { get; set; } = null!;
    public ApplicationUser Usuario { get; set; } = null!;

    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;
}
