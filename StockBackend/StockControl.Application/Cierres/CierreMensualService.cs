using Microsoft.EntityFrameworkCore;
using StockControl.Application.Auditoria;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Cierres;

public class CierreMensualService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditoriaService? auditoria = null) : ICierreMensualService
{
    public async Task<List<CierreMensualDto>> ListarAsync(FiltroCierresMensuales filtro, CancellationToken ct = default)
    {
        AsegurarGerencia();

        if (filtro.HotelId is { } hotelId && !currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var query = db.CierresMensuales
            .Include(c => c.Hotel)
            .AsQueryable();

        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
        {
            var hoteles = currentUser.HotelesPermitidos;
            query = query.Where(c => hoteles.Contains(c.HotelId));
        }

        if (filtro.HotelId is not null) query = query.Where(c => c.HotelId == filtro.HotelId);
        if (filtro.Anio is not null) query = query.Where(c => c.Anio == filtro.Anio);
        if (filtro.Mes is not null) query = query.Where(c => c.Mes == filtro.Mes);

        var cierres = await query
            .OrderByDescending(c => c.Anio)
            .ThenByDescending(c => c.Mes)
            .ThenBy(c => c.Hotel.Nombre)
            .Take(120)
            .ToListAsync(ct);

        return cierres.Select(Mapear).ToList();
    }

    public async Task<CierreMensualDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        AsegurarGerencia();

        var cierre = await db.CierresMensuales
            .Include(c => c.Hotel)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cierre is null) return null;

        if (!currentUser.PuedeAccederHotel(cierre.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        return Mapear(cierre);
    }

    public async Task<CierreMensualDto> PreviewAsync(int hotelId, int anio, int mes, CancellationToken ct = default)
    {
        AsegurarGerencia();
        ValidarPeriodo(anio, mes);

        if (!currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var snapshot = await CalcularSnapshotAsync(hotelId, anio, mes, ct);
        return snapshot with
        {
            Id = 0,
            Estado = "Preliminar",
            FechaCierre = DateTime.UtcNow,
            CreadoEn = DateTime.UtcNow,
            CreadoPor = currentUser.UserName ?? "sistema",
        };
    }

    public async Task<CierreMensualDto> CerrarAsync(CerrarMesRequest req, CancellationToken ct = default)
    {
        AsegurarGerencia();
        ValidarPeriodo(req.Anio, req.Mes);

        if (!currentUser.PuedeAccederHotel(req.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var hotel = await db.Hoteles
            .Where(h => h.Id == req.HotelId && h.Activo)
            .Select(h => new { h.Id, h.Nombre })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Hotel invalido o inactivo.");

        var existe = await db.CierresMensuales.AnyAsync(
            c => c.HotelId == req.HotelId
                 && c.Anio == req.Anio
                 && c.Mes == req.Mes
                 && c.Estado == EstadoCierreMensual.Cerrado, ct);
        if (existe)
            throw new InvalidOperationException("Este hotel ya tiene cierre para ese mes.");

        var snapshot = await CalcularSnapshotAsync(req.HotelId, req.Anio, req.Mes, ct);
        var cierre = new CierreMensual
        {
            HotelId = hotel.Id,
            Anio = req.Anio,
            Mes = req.Mes,
            Estado = EstadoCierreMensual.Cerrado,
            ComprasTotal = snapshot.ComprasTotal,
            DocumentosCompra = snapshot.DocumentosCompra,
            ValorInventarioEstimado = snapshot.ValorInventarioEstimado,
            ProductosEnRiesgo = snapshot.ProductosEnRiesgo,
            ValorFaltanteEstimado = snapshot.ValorFaltanteEstimado,
            ValorMermasEstimado = snapshot.ValorMermasEstimado,
            MovimientosMerma = snapshot.MovimientosMerma,
            ValorAjustesEstimado = snapshot.ValorAjustesEstimado,
            MovimientosAjuste = snapshot.MovimientosAjuste,
            ConteosFisicos = snapshot.ConteosFisicos,
            ValorDiferenciasConteo = snapshot.ValorDiferenciasConteo,
            SaldoCuentasPorPagar = snapshot.SaldoCuentasPorPagar,
            SaldoCuentasVencido = snapshot.SaldoCuentasVencido,
            DocumentosVencidos = snapshot.DocumentosVencidos,
            FechaCierre = DateTime.UtcNow,
            Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones.Trim(),
        };

        db.CierresMensuales.Add(cierre);
        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Cierre mensual creado",
            "CierreMensual",
            cierre.Id,
            cierre.HotelId,
            $"Cierre {cierre.Mes}/{cierre.Anio} creado para {hotel.Nombre}",
            $"Compras Q{cierre.ComprasTotal:N2}; inventario Q{cierre.ValorInventarioEstimado:N2}; CXP Q{cierre.SaldoCuentasPorPagar:N2}",
            ct);

        return (await ObtenerAsync(cierre.Id, ct))!;
    }

    public async Task<CierreMensualDto?> AnularAsync(int id, AnularCierreMensualRequest req, CancellationToken ct = default)
    {
        AsegurarGerencia();

        var cierre = await db.CierresMensuales
            .Include(c => c.Hotel)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cierre is null) return null;

        if (!currentUser.PuedeAccederHotel(cierre.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        if (cierre.Estado == EstadoCierreMensual.Anulado)
            throw new InvalidOperationException("Este cierre ya esta anulado.");

        cierre.Estado = EstadoCierreMensual.Anulado;
        cierre.Observaciones = ConstruirObservacionAnulacion(cierre.Observaciones, req.Motivo, currentUser.UserName);

        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Cierre mensual anulado",
            "CierreMensual",
            cierre.Id,
            cierre.HotelId,
            $"Cierre {cierre.Mes}/{cierre.Anio} anulado para {cierre.Hotel.Nombre}",
            string.IsNullOrWhiteSpace(req.Motivo) ? null : req.Motivo,
            ct);
        return Mapear(cierre);
    }

    private async Task<CierreMensualDto> CalcularSnapshotAsync(int hotelId, int anio, int mes, CancellationToken ct)
    {
        var hotel = await db.Hoteles
            .Where(h => h.Id == hotelId && h.Activo)
            .Select(h => new { h.Id, h.Nombre })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Hotel invalido o inactivo.");

        var inicio = new DateOnly(anio, mes, 1);
        var fin = inicio.AddMonths(1);
        var corte = fin.AddDays(-1);

        var detallesPeriodo = await db.Detalles
            .Include(d => d.DocumentoCompra)
            .Where(d => d.DocumentoCompra.HotelId == hotelId
                        && d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido
                        && d.DocumentoCompra.Fecha >= inicio
                        && d.DocumentoCompra.Fecha < fin)
            .ToListAsync(ct);

        var comprasTotal = detallesPeriodo.Sum(d => d.Cantidad * d.PrecioUnitario);
        var documentosCompra = detallesPeriodo.Select(d => d.DocumentoCompraId).Distinct().Count();

        var inventario = await CalcularInventarioAsync(hotelId, fin, ct);
        var ultimosPrecios = await CargarUltimosPreciosAsync(hotelId, fin, ct);

        var valorInventario = inventario.Sum(i => Math.Max(0, i.Value) * ultimosPrecios.GetValueOrDefault(i.Key));

        var stockMinimos = await db.StockMinimos
            .Where(s => s.HotelId == hotelId)
            .ToListAsync(ct);

        var productosEnRiesgo = 0;
        var valorFaltante = 0m;
        foreach (var stock in stockMinimos.Where(s => s.CantidadMinimaBase > 0))
        {
            var existencia = inventario.GetValueOrDefault(stock.ProductoId);
            if (existencia >= stock.CantidadMinimaBase) continue;

            var faltante = stock.CantidadMinimaBase - existencia;
            productosEnRiesgo++;
            valorFaltante += faltante * ultimosPrecios.GetValueOrDefault(stock.ProductoId);
        }

        var movimientosPeriodo = await db.Movimientos
            .Where(m => m.HotelId == hotelId && m.Fecha >= inicio && m.Fecha < fin)
            .ToListAsync(ct);

        var mermas = movimientosPeriodo.Where(m => m.Tipo == TipoMovimiento.Merma).ToList();
        var ajustes = movimientosPeriodo.Where(m => m.Tipo == TipoMovimiento.Ajuste).ToList();
        var valorMermas = mermas.Sum(m => m.CantidadBase * ultimosPrecios.GetValueOrDefault(m.ProductoId));
        var valorAjustes = ajustes.Sum(m => Math.Abs(m.CantidadBase) * ultimosPrecios.GetValueOrDefault(m.ProductoId));

        var conteos = await db.ConteosInventario
            .Include(c => c.Detalles)
            .Where(c => c.HotelId == hotelId
                        && c.Estado != EstadoConteoInventario.Anulado
                        && c.Fecha >= inicio
                        && c.Fecha < fin)
            .ToListAsync(ct);

        var documentosCxP = await db.Documentos
            .Include(d => d.Proveedor)
            .Include(d => d.Detalles)
            .Include(d => d.Pagos)
            .Where(d => d.HotelId == hotelId
                        && d.Estado == EstadoDocumentoCompra.Recibido
                        && (d.Observaciones ?? "") != DocumentoCompra.ObservacionImportadoExcel
                        && d.Proveedor.Nombre != Proveedor.NombreProveedorImportacionExcel
                        && d.Fecha < fin)
            .ToListAsync(ct);

        var saldos = documentosCxP
            .Select(d =>
            {
                var bruto = d.Detalles.Sum(x => x.Cantidad * x.PrecioUnitario);
                var neto = Math.Max(0, bruto - d.Retencion);
                var pagado = d.Pagos.Where(p => p.Fecha < fin).Sum(p => p.Monto);
                var saldo = Math.Max(0, neto - pagado);
                var vencido = saldo > 0 && d.Fecha.AddDays(d.Proveedor.DiasCredito) < corte;
                return new { Saldo = saldo, Vencido = vencido };
            })
            .Where(c => c.Saldo > 0)
            .ToList();

        return new CierreMensualDto(
            0,
            hotel.Id,
            hotel.Nombre,
            anio,
            mes,
            EstadoCierreMensual.Cerrado.ToString(),
            Redondear(comprasTotal),
            documentosCompra,
            Redondear(valorInventario),
            productosEnRiesgo,
            Redondear(valorFaltante),
            Redondear(valorMermas),
            mermas.Count,
            Redondear(valorAjustes),
            ajustes.Count,
            conteos.Count,
            Redondear(conteos.Sum(c => c.Detalles.Sum(d => Math.Abs(d.ValorDiferenciaEstimado)))),
            Redondear(saldos.Sum(s => s.Saldo)),
            Redondear(saldos.Where(s => s.Vencido).Sum(s => s.Saldo)),
            saldos.Count(s => s.Vencido),
            DateTime.UtcNow,
            null,
            DateTime.UtcNow,
            currentUser.UserName ?? "sistema");
    }

    private async Task<Dictionary<int, decimal>> CalcularInventarioAsync(int hotelId, DateOnly finExclusivo, CancellationToken ct)
    {
        var compras = await db.Detalles
            .Where(d => d.DocumentoCompra.HotelId == hotelId
                        && d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido
                        && d.DocumentoCompra.Fecha < finExclusivo)
            .GroupBy(d => d.ProductoId)
            .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(d => d.Cantidad * d.FactorABase) })
            .ToListAsync(ct);

        var movimientos = await db.Movimientos
            .Where(m => m.HotelId == hotelId && m.Fecha < finExclusivo)
            .GroupBy(m => new { m.ProductoId, m.Tipo })
            .Select(g => new { g.Key.ProductoId, g.Key.Tipo, Cantidad = g.Sum(m => m.CantidadBase) })
            .ToListAsync(ct);

        var inventario = new Dictionary<int, decimal>();
        foreach (var compra in compras)
            inventario[compra.ProductoId] = inventario.GetValueOrDefault(compra.ProductoId) + compra.Cantidad;

        foreach (var movimiento in movimientos)
        {
            var efecto = movimiento.Tipo switch
            {
                TipoMovimiento.Entrada => movimiento.Cantidad,
                TipoMovimiento.Salida => -movimiento.Cantidad,
                TipoMovimiento.Merma => -movimiento.Cantidad,
                TipoMovimiento.Ajuste => movimiento.Cantidad,
                _ => 0m,
            };
            inventario[movimiento.ProductoId] = inventario.GetValueOrDefault(movimiento.ProductoId) + efecto;
        }

        return inventario;
    }

    private async Task<Dictionary<int, decimal>> CargarUltimosPreciosAsync(int hotelId, DateOnly finExclusivo, CancellationToken ct)
    {
        var precios = await db.Detalles
            .Where(d => d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido
                        && d.DocumentoCompra.Fecha < finExclusivo
                        && d.FactorABase > 0)
            .Select(d => new
            {
                d.Id,
                d.ProductoId,
                d.DocumentoCompra.HotelId,
                d.DocumentoCompra.Fecha,
                PrecioBase = d.PrecioUnitario / d.FactorABase,
            })
            .ToListAsync(ct);

        return precios
            .GroupBy(d => d.ProductoId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(d => d.HotelId == hotelId)
                    .ThenByDescending(d => d.Fecha)
                    .ThenByDescending(d => d.Id)
                    .First()
                    .PrecioBase);
    }

    private static CierreMensualDto Mapear(CierreMensual c) => new(
        c.Id,
        c.HotelId,
        c.Hotel.Nombre,
        c.Anio,
        c.Mes,
        c.Estado.ToString(),
        Redondear(c.ComprasTotal),
        c.DocumentosCompra,
        Redondear(c.ValorInventarioEstimado),
        c.ProductosEnRiesgo,
        Redondear(c.ValorFaltanteEstimado),
        Redondear(c.ValorMermasEstimado),
        c.MovimientosMerma,
        Redondear(c.ValorAjustesEstimado),
        c.MovimientosAjuste,
        c.ConteosFisicos,
        Redondear(c.ValorDiferenciasConteo),
        Redondear(c.SaldoCuentasPorPagar),
        Redondear(c.SaldoCuentasVencido),
        c.DocumentosVencidos,
        c.FechaCierre,
        c.Observaciones,
        c.CreadoEn,
        c.CreadoPor);

    private void AsegurarGerencia()
    {
        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
            throw new UnauthorizedAccessException("Solo Admin o Gerencia pueden consultar cierres mensuales.");
    }

    private static void ValidarPeriodo(int anio, int mes)
    {
        if (anio is < 2020 or > 2100)
            throw new InvalidOperationException("Anio invalido para cierre mensual.");
        if (mes is < 1 or > 12)
            throw new InvalidOperationException("Mes invalido para cierre mensual.");
    }

    private static string ConstruirObservacionAnulacion(string? actual, string? motivo, string? usuario)
    {
        var detalle = string.IsNullOrWhiteSpace(motivo) ? "Sin motivo especificado" : motivo.Trim();
        var registro = $"Anulado {DateTime.UtcNow:yyyy-MM-dd HH:mm} por {usuario ?? "sistema"}: {detalle}";
        var observacion = string.IsNullOrWhiteSpace(actual) ? registro : $"{actual.Trim()} | {registro}";
        return observacion.Length <= 500 ? observacion : observacion[..500];
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

    private static decimal Redondear(decimal valor) => Math.Round(valor, 2);
}
