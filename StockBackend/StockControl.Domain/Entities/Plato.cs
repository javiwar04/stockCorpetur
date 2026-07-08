using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>
/// FASE 3. Plato del menú, para costeo de recetas. Su costo se calcula en tiempo
/// real a partir de los precios vigentes de sus ingredientes.
/// </summary>
public class Plato : EntidadBase
{
    public string Nombre { get; set; } = null!;
    public decimal? PrecioVenta { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<RecetaDetalle> Ingredientes { get; set; } = new List<RecetaDetalle>();
}
