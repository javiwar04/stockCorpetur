using Microsoft.EntityFrameworkCore;
using StockControl.Application.Auditoria;
using StockControl.Application.Cierres;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Compras;

public class DocumentoCompraService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICierreMensualGuard? cierreGuard = null,
    IAuditoriaService? auditoria = null) : IDocumentoCompraService
{
    private readonly ICierreMensualGuard _cierreGuard = cierreGuard ?? new CierreMensualGuard(db);

    public async Task<List<DocumentoCompraResumenDto>> ListarAsync(FiltroDocumentos filtro, CancellationToken ct = default)
    {
        if (filtro.HotelId is { } hotelId && !currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var query = db.Documentos
            .Include(d => d.Hotel)
            .Include(d => d.Proveedor)
            .Include(d => d.Detalles)
            .AsQueryable();

        query = AplicarScopingHotel(query);

        if (filtro.HotelId is not null) query = query.Where(d => d.HotelId == filtro.HotelId);
        if (filtro.ProveedorId is not null) query = query.Where(d => d.ProveedorId == filtro.ProveedorId);
        if (!string.IsNullOrWhiteSpace(filtro.TipoCompra))
        {
            var tipoCompra = ParsearTipoCompra(filtro.TipoCompra);
            query = query.Where(d => d.TipoCompra == tipoCompra);
        }
        if (filtro.Desde is not null) query = query.Where(d => d.Fecha >= filtro.Desde);
        if (filtro.Hasta is not null) query = query.Where(d => d.Fecha <= filtro.Hasta);

        var documentos = await query.OrderByDescending(d => d.Fecha).ToListAsync(ct);

        return documentos.Select(d => new DocumentoCompraResumenDto(
            d.Id, d.Fecha, d.NumeroDocumento, d.NumeroPedido, d.HotelId, d.Hotel.Nombre, d.ProveedorId, d.Proveedor.Nombre,
            d.Estado.ToString(), d.TipoCompra.ToString(), d.Total)).ToList();
    }

    public async Task<DocumentoCompraDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var d = await db.Documentos
            .Include(x => x.Hotel)
            .Include(x => x.Proveedor)
            .Include(x => x.Detalles).ThenInclude(det => det.Producto)
            .Include(x => x.Detalles).ThenInclude(det => det.Unidad)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (d is null) return null;
        if (!currentUser.PuedeAccederHotel(d.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        return Mapear(d);
    }

    public async Task<DocumentoCompraDto> CrearAsync(CrearDocumentoCompraRequest req, CancellationToken ct = default)
    {
        if (!currentUser.PuedeAccederHotel(req.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        if (req.Detalles.Count == 0)
            throw new InvalidOperationException("El documento debe tener al menos un producto.");

        var numeroDocumento = ValidarTextoObligatorio(req.NumeroDocumento, "numero de documento");
        var numeroPedido = ValidarTextoObligatorio(req.NumeroPedido, "numero de pedido");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(req.HotelId, req.Fecha, "registrar documentos", ct);

        var estado = ParsearEstadoParaGuardar(req.Estado, EstadoDocumentoCompra.Recibido);

        var numeroRepetido = await db.Documentos.AnyAsync(
            x => x.HotelId == req.HotelId && x.NumeroDocumento == numeroDocumento, ct);
        if (numeroRepetido)
            throw new InvalidOperationException("Ya existe un documento con ese número para este hotel.");

        var documento = new DocumentoCompra
        {
            Fecha = req.Fecha,
            NumeroDocumento = numeroDocumento,
            NumeroPedido = numeroPedido,
            HotelId = req.HotelId,
            ProveedorId = req.ProveedorId,
            Estado = estado,
            TipoCompra = ParsearTipoCompra(req.TipoCompra),
            Retencion = req.Retencion,
            Observaciones = req.Observaciones,
        };

        foreach (var linea in req.Detalles)
        {
            if (linea.Cantidad <= 0) throw new InvalidOperationException("La cantidad debe ser mayor a cero.");
            if (linea.PrecioUnitario < 0) throw new InvalidOperationException("El precio no puede ser negativo.");

            var conversion = await db.Conversiones.FirstOrDefaultAsync(
                c => c.ProductoId == linea.ProductoId && c.UnidadId == linea.UnidadId, ct)
                ?? throw new InvalidOperationException("No existe conversión configurada para ese producto y unidad.");

            documento.Detalles.Add(new DetalleCompra
            {
                ProductoId = linea.ProductoId,
                UnidadId = linea.UnidadId,
                Cantidad = linea.Cantidad,
                PrecioUnitario = linea.PrecioUnitario,
                FactorABase = conversion.FactorABase,
            });
        }

        db.Documentos.Add(documento);
        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Documento creado",
            "DocumentoCompra",
            documento.Id,
            documento.HotelId,
            $"Documento {documento.NumeroDocumento} creado",
            $"Estado {documento.Estado}; tipo {documento.TipoCompra}; total Q{documento.Total:N2}",
            ct);

        return (await ObtenerAsync(documento.Id, ct))!;
    }

    public async Task<DocumentoCompraDto?> ActualizarAsync(int id, CrearDocumentoCompraRequest req, CancellationToken ct = default)
    {
        var documento = await db.Documentos.Include(d => d.Detalles).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (documento is null) return null;

        // Debe poder acceder tanto al hotel actual del documento como al nuevo.
        if (!currentUser.PuedeAccederHotel(documento.HotelId) || !currentUser.PuedeAccederHotel(req.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        if (req.Detalles.Count == 0)
            throw new InvalidOperationException("El documento debe tener al menos un producto.");

        if (documento.Estado == EstadoDocumentoCompra.Anulado)
            throw new InvalidOperationException("No se puede editar un documento anulado.");

        var numeroDocumento = ValidarTextoObligatorio(req.NumeroDocumento, "numero de documento");
        var numeroPedido = ValidarTextoObligatorio(req.NumeroPedido, "numero de pedido");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(documento.HotelId, documento.Fecha, "editar documentos", ct);
        await _cierreGuard.AsegurarPeriodoAbiertoAsync(req.HotelId, req.Fecha, "editar documentos", ct);

        var estado = req.Estado is null
            ? documento.Estado
            : ParsearEstadoParaGuardar(req.Estado, documento.Estado);

        var numeroRepetido = await db.Documentos.AnyAsync(
            x => x.Id != id && x.HotelId == req.HotelId && x.NumeroDocumento == numeroDocumento, ct);
        if (numeroRepetido)
            throw new InvalidOperationException("Ya existe otro documento con ese número para este hotel.");

        documento.Fecha = req.Fecha;
        documento.NumeroDocumento = numeroDocumento;
        documento.NumeroPedido = numeroPedido;
        documento.HotelId = req.HotelId;
        documento.ProveedorId = req.ProveedorId;
        documento.Estado = estado;
        documento.TipoCompra = ParsearTipoCompra(req.TipoCompra, documento.TipoCompra);
        documento.Retencion = req.Retencion;
        documento.Observaciones = req.Observaciones;

        // Reemplaza las líneas: las anteriores se eliminan (cascade) y se agregan las nuevas.
        documento.Detalles.Clear();
        foreach (var linea in req.Detalles)
        {
            if (linea.Cantidad <= 0) throw new InvalidOperationException("La cantidad debe ser mayor a cero.");
            if (linea.PrecioUnitario < 0) throw new InvalidOperationException("El precio no puede ser negativo.");

            var conversion = await db.Conversiones.FirstOrDefaultAsync(
                c => c.ProductoId == linea.ProductoId && c.UnidadId == linea.UnidadId, ct)
                ?? throw new InvalidOperationException("No existe conversión configurada para ese producto y unidad.");

            documento.Detalles.Add(new DetalleCompra
            {
                ProductoId = linea.ProductoId,
                UnidadId = linea.UnidadId,
                Cantidad = linea.Cantidad,
                PrecioUnitario = linea.PrecioUnitario,
                FactorABase = conversion.FactorABase,
            });
        }

        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Documento actualizado",
            "DocumentoCompra",
            documento.Id,
            documento.HotelId,
            $"Documento {documento.NumeroDocumento} actualizado",
            $"Estado {documento.Estado}; tipo {documento.TipoCompra}; total Q{documento.Total:N2}",
            ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<DocumentoCompraDto?> RecibirAsync(int id, CancellationToken ct = default)
    {
        var documento = await db.Documentos.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (documento is null) return null;

        if (!currentUser.PuedeAccederHotel(documento.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        if (documento.Estado == EstadoDocumentoCompra.Anulado)
            throw new InvalidOperationException("No se puede recibir un documento anulado.");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(documento.HotelId, documento.Fecha, "recibir documentos", ct);

        documento.Estado = EstadoDocumentoCompra.Recibido;
        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Documento recibido",
            "DocumentoCompra",
            documento.Id,
            documento.HotelId,
            $"Documento {documento.NumeroDocumento} marcado como recibido",
            null,
            ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<DocumentoCompraDto?> AnularAsync(int id, CancellationToken ct = default)
    {
        var documento = await db.Documentos.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (documento is null) return null;

        if (!currentUser.PuedeAccederHotel(documento.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(documento.HotelId, documento.Fecha, "anular documentos", ct);

        documento.Estado = EstadoDocumentoCompra.Anulado;
        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Documento anulado",
            "DocumentoCompra",
            documento.Id,
            documento.HotelId,
            $"Documento {documento.NumeroDocumento} anulado",
            null,
            ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<bool> EliminarAsync(int id, CancellationToken ct = default)
    {
        var documento = await db.Documentos.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (documento is null) return false;

        if (!currentUser.PuedeAccederHotel(documento.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(documento.HotelId, documento.Fecha, "eliminar documentos", ct);

        var hotelId = documento.HotelId;
        var numero = documento.NumeroDocumento;
        db.Documentos.Remove(documento);
        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Documento eliminado",
            "DocumentoCompra",
            id,
            hotelId,
            $"Documento {numero} eliminado",
            null,
            ct);
        return true;
    }

    /// <summary>Admin/Gerencia ven todo; un Digitador solo ve sus hoteles asignados.</summary>
    private IQueryable<DocumentoCompra> AplicarScopingHotel(IQueryable<DocumentoCompra> query)
    {
        if (currentUser.EsAdmin || currentUser.EsGerencia) return query;
        var hoteles = currentUser.HotelesPermitidos;
        return query.Where(d => hoteles.Contains(d.HotelId));
    }

    private static DocumentoCompraDto Mapear(DocumentoCompra d) => new(
        d.Id, d.Fecha, d.NumeroDocumento, d.NumeroPedido, d.HotelId, d.Hotel.Nombre, d.ProveedorId, d.Proveedor.Nombre,
        d.Estado.ToString(), d.TipoCompra.ToString(), d.Retencion, d.Observaciones, d.Total,
        d.Detalles.Select(det => new DetalleCompraDto(
            det.Id, det.ProductoId, det.Producto.Nombre, det.UnidadId, det.Unidad.Nombre,
            det.Cantidad, det.PrecioUnitario, det.Total)).ToList());

    private static string ValidarTextoObligatorio(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException($"El {campo} es obligatorio.");

        return valor.Trim();
    }

    private static EstadoDocumentoCompra ParsearEstadoParaGuardar(string? valor, EstadoDocumentoCompra predeterminado)
    {
        if (string.IsNullOrWhiteSpace(valor)) return predeterminado;

        if (!Enum.TryParse<EstadoDocumentoCompra>(valor, ignoreCase: true, out var estado))
            throw new InvalidOperationException($"Estado de documento invalido: {valor}");

        if (estado == EstadoDocumentoCompra.Anulado)
            throw new InvalidOperationException("Usa la accion de anulacion para anular documentos.");

        return estado;
    }

    private static TipoCompra ParsearTipoCompra(string? valor, TipoCompra predeterminado = TipoCompra.Ordinaria)
    {
        if (string.IsNullOrWhiteSpace(valor)) return predeterminado;

        return Enum.TryParse<TipoCompra>(valor, ignoreCase: true, out var tipo)
            ? tipo
            : throw new InvalidOperationException($"Tipo de compra invalido: {valor}");
    }

    private async Task AuditarAsync(
        string accion,
        string entidad,
        int? entidadId,
        int? hotelId,
        string resumen,
        string? detalle,
        CancellationToken ct)
    {
        if (auditoria is null) return;
        await auditoria.RegistrarAsync(new RegistrarAuditoriaRequest(accion, entidad, entidadId, hotelId, resumen, detalle), ct);
    }
}
