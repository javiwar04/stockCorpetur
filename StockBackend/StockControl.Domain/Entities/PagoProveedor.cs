using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>Abono o pago aplicado a un documento de compra recibido.</summary>
public class PagoProveedor : EntidadBase
{
    public int DocumentoCompraId { get; set; }
    public DocumentoCompra DocumentoCompra { get; set; } = null!;

    public int ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;

    public DateOnly Fecha { get; set; }
    public decimal Monto { get; set; }
    public string MetodoPago { get; set; } = "Transferencia";
    public string? Referencia { get; set; }
    public string? Observaciones { get; set; }
}
