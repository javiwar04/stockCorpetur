using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>Umbral mínimo de existencia para un producto dentro de un hotel, expresado en unidad base.</summary>
public class StockMinimo : EntidadBase
{
    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public decimal CantidadMinimaBase { get; set; }
}
