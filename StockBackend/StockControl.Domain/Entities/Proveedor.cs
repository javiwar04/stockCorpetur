using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>
/// Proveedor de los productos. No existe en el Excel original: se captura desde
/// la app y en la importación histórica se marca como "Desconocido".
/// </summary>
public class Proveedor : EntidadBase
{
    public string Nombre { get; set; } = null!;
    public string? Nit { get; set; }
    public string? Telefono { get; set; }
    public int DiasCredito { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<DocumentoCompra> Documentos { get; set; } = new List<DocumentoCompra>();
    public ICollection<PagoProveedor> Pagos { get; set; } = new List<PagoProveedor>();
}
