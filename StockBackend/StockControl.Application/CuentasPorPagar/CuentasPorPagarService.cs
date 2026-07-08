using Microsoft.EntityFrameworkCore;
using StockControl.Application.Auditoria;
using StockControl.Application.Cierres;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.CuentasPorPagar;

public class CuentasPorPagarService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICierreMensualGuard? cierreGuard = null,
    IAuditoriaService? auditoria = null) : ICuentasPorPagarService
{
    private readonly ICierreMensualGuard _cierreGuard = cierreGuard ?? new CierreMensualGuard(db);

    public async Task<CuentasPorPagarResultadoDto> ListarAsync(FiltroCuentasPorPagar filtro, CancellationToken ct = default)
    {
        if (filtro.HotelId is { } hotelId && !currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var query = db.Documentos
            .Include(d => d.Hotel)
            .Include(d => d.Proveedor)
            .Include(d => d.Detalles)
            .Include(d => d.Pagos).ThenInclude(p => p.Proveedor)
            .Where(d => d.Estado == EstadoDocumentoCompra.Recibido)
            .AsQueryable();

        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
        {
            var hoteles = currentUser.HotelesPermitidos;
            query = query.Where(d => hoteles.Contains(d.HotelId));
        }

        if (filtro.HotelId is not null) query = query.Where(d => d.HotelId == filtro.HotelId);
        if (filtro.ProveedorId is not null) query = query.Where(d => d.ProveedorId == filtro.ProveedorId);
        if (filtro.Desde is not null) query = query.Where(d => d.Fecha >= filtro.Desde);
        if (filtro.Hasta is not null) query = query.Where(d => d.Fecha <= filtro.Hasta);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var cuentas = (await query
                .OrderBy(d => d.Fecha)
                .ThenBy(d => d.Proveedor.Nombre)
                .ThenBy(d => d.NumeroDocumento)
                .ToListAsync(ct))
            .Select(d => MapearCuenta(d, hoy))
            .Where(c => !filtro.SoloPendientes || c.Saldo > 0)
            .OrderBy(c => c.Estado == "Vencido" ? 0 : c.Estado == "Parcial" ? 1 : c.Estado == "Pendiente" ? 2 : 3)
            .ThenBy(c => c.FechaVencimiento)
            .ThenBy(c => c.ProveedorNombre)
            .ToList();

        var pendientes = cuentas.Where(c => c.Saldo > 0).ToList();
        var resumen = new ResumenCuentasPorPagarDto(
            Math.Round(cuentas.Sum(c => c.NetoAPagar), 2),
            Math.Round(cuentas.Sum(c => c.Pagado), 2),
            Math.Round(cuentas.Sum(c => c.Saldo), 2),
            Math.Round(cuentas.Where(c => c.Estado == "Vencido").Sum(c => c.Saldo), 2),
            cuentas.Count(c => c.Saldo > 0),
            cuentas.Count(c => c.Estado == "Vencido"),
            Math.Round(pendientes.Where(c => c.FechaVencimiento >= hoy).Sum(c => c.Saldo), 2),
            Math.Round(pendientes.Where(c => DiasVencido(c.FechaVencimiento, hoy) is >= 1 and <= 30).Sum(c => c.Saldo), 2),
            Math.Round(pendientes.Where(c => DiasVencido(c.FechaVencimiento, hoy) is >= 31 and <= 60).Sum(c => c.Saldo), 2),
            Math.Round(pendientes.Where(c => DiasVencido(c.FechaVencimiento, hoy) >= 61).Sum(c => c.Saldo), 2));

        return new CuentasPorPagarResultadoDto(resumen, cuentas);
    }

    public async Task<PagoProveedorDto> RegistrarPagoAsync(RegistrarPagoProveedorRequest req, CancellationToken ct = default)
    {
        if (req.Monto <= 0)
            throw new InvalidOperationException("El monto del pago debe ser mayor a cero.");

        var documento = await db.Documentos
            .Include(d => d.Hotel)
            .Include(d => d.Proveedor)
            .Include(d => d.Detalles)
            .Include(d => d.Pagos).ThenInclude(p => p.Proveedor)
            .FirstOrDefaultAsync(d => d.Id == req.DocumentoCompraId, ct)
            ?? throw new InvalidOperationException("El documento no existe.");

        if (!currentUser.PuedeAccederHotel(documento.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(documento.HotelId, req.Fecha, "registrar pagos", ct);

        if (documento.Estado != EstadoDocumentoCompra.Recibido)
            throw new InvalidOperationException("Solo se pueden pagar documentos recibidos.");

        var cuenta = MapearCuenta(documento, DateOnly.FromDateTime(DateTime.UtcNow));
        if (req.Monto > cuenta.Saldo)
            throw new InvalidOperationException("El pago excede el saldo pendiente del documento.");

        var pago = new PagoProveedor
        {
            DocumentoCompraId = documento.Id,
            ProveedorId = documento.ProveedorId,
            Fecha = req.Fecha,
            Monto = req.Monto,
            MetodoPago = string.IsNullOrWhiteSpace(req.MetodoPago) ? "Transferencia" : req.MetodoPago.Trim(),
            Referencia = string.IsNullOrWhiteSpace(req.Referencia) ? null : req.Referencia.Trim(),
            Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones.Trim(),
        };

        db.PagosProveedor.Add(pago);
        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Pago proveedor registrado",
            "PagoProveedor",
            pago.Id,
            documento.HotelId,
            $"Pago de Q{pago.Monto:N2} registrado a {documento.Proveedor.Nombre}",
            $"Documento {documento.NumeroDocumento}; fecha {pago.Fecha:dd/MM/yyyy}; metodo {pago.MetodoPago}",
            ct);

        pago.Proveedor = documento.Proveedor;
        return MapearPago(pago);
    }

    public async Task<bool> EliminarPagoAsync(int id, CancellationToken ct = default)
    {
        var pago = await db.PagosProveedor
            .Include(p => p.DocumentoCompra)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pago is null) return false;

        if (!currentUser.PuedeAccederHotel(pago.DocumentoCompra.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(pago.DocumentoCompra.HotelId, pago.Fecha, "eliminar pagos", ct);

        var hotelId = pago.DocumentoCompra.HotelId;
        var monto = pago.Monto;
        var fecha = pago.Fecha;
        var documentoId = pago.DocumentoCompraId;
        db.PagosProveedor.Remove(pago);
        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Pago proveedor eliminado",
            "PagoProveedor",
            id,
            hotelId,
            $"Pago de Q{monto:N2} eliminado",
            $"DocumentoId {documentoId}; fecha {fecha:dd/MM/yyyy}",
            ct);
        return true;
    }

    private static CuentaPorPagarDto MapearCuenta(DocumentoCompra d, DateOnly hoy)
    {
        var bruto = d.Total;
        var neto = Math.Max(0, bruto - d.Retencion);
        var pagado = d.Pagos.Sum(p => p.Monto);
        var saldo = Math.Max(0, neto - pagado);
        var fechaVencimiento = d.Fecha.AddDays(d.Proveedor.DiasCredito);
        var estado = EstadoCuenta(saldo, pagado, fechaVencimiento, hoy);

        return new CuentaPorPagarDto(
            d.Id,
            d.Fecha,
            fechaVencimiento,
            d.NumeroDocumento,
            d.HotelId,
            d.Hotel.Nombre,
            d.ProveedorId,
            d.Proveedor.Nombre,
            d.Proveedor.DiasCredito,
            Math.Round(bruto, 2),
            Math.Round(d.Retencion, 2),
            Math.Round(neto, 2),
            Math.Round(pagado, 2),
            Math.Round(saldo, 2),
            estado,
            d.Pagos.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).Select(MapearPago).ToList());
    }

    private static PagoProveedorDto MapearPago(PagoProveedor p) => new(
        p.Id,
        p.DocumentoCompraId,
        p.ProveedorId,
        p.Proveedor.Nombre,
        p.Fecha,
        Math.Round(p.Monto, 2),
        p.MetodoPago,
        p.Referencia,
        p.Observaciones,
        p.CreadoEn,
        p.CreadoPor);

    private static string EstadoCuenta(decimal saldo, decimal pagado, DateOnly fechaVencimiento, DateOnly hoy)
    {
        if (saldo <= 0) return "Pagado";
        if (fechaVencimiento < hoy) return "Vencido";
        if (pagado > 0) return "Parcial";
        return "Pendiente";
    }

    private static int DiasVencido(DateOnly fechaVencimiento, DateOnly hoy) => hoy.DayNumber - fechaVencimiento.DayNumber;

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
