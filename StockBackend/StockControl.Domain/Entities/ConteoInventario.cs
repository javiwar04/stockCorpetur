using StockControl.Domain.Common;
using StockControl.Domain.Enums;

namespace StockControl.Domain.Entities;

/// <summary>Captura de conteo fisico de inventario para comparar sistema contra realidad.</summary>
public class ConteoInventario : EntidadBase
{
    public DateOnly Fecha { get; set; }

    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public EstadoConteoInventario Estado { get; set; } = EstadoConteoInventario.Registrado;

    public string? Observaciones { get; set; }

    public DateTime? AjustesAplicadosEn { get; set; }
    public string? AjustesAplicadosPor { get; set; }

    public ICollection<ConteoInventarioDetalle> Detalles { get; set; } = new List<ConteoInventarioDetalle>();
}
