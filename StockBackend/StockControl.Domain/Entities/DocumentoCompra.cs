using StockControl.Domain.Common;
using StockControl.Domain.Enums;

namespace StockControl.Domain.Entities;

/// <summary>
/// Documento de ingreso de vegetales/frutas/etc. Es la unidad que el digitador
/// registra: fecha, proveedor, hotel, número de documento y sus líneas de detalle.
/// </summary>
public class DocumentoCompra : EntidadBase
{
    public DateOnly Fecha { get; set; }

    /// <summary>Número de documento / factura (obligatorio).</summary>
    public string NumeroDocumento { get; set; } = null!;

    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public int ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;

    /// <summary>Retención aplicada al documento (si la hay).</summary>
    public decimal Retencion { get; set; }

    public EstadoDocumentoCompra Estado { get; set; } = EstadoDocumentoCompra.Recibido;

    public string? Observaciones { get; set; }

    public ICollection<DetalleCompra> Detalles { get; set; } = new List<DetalleCompra>();
    public ICollection<PagoProveedor> Pagos { get; set; } = new List<PagoProveedor>();

    /// <summary>Suma de los totales de línea. Calculado en la capa de aplicación / consultas.</summary>
    public decimal Total => Detalles.Sum(d => d.Total);
}
