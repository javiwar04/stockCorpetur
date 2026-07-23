using Microsoft.EntityFrameworkCore;
using StockControl.Application.Auditoria;
using StockControl.Application.Cierres;
using StockControl.Application.Common;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Conteos;

public class ConteoInventarioService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICierreMensualGuard? cierreGuard = null,
    IAuditoriaService? auditoria = null) : IConteoInventarioService
{
    private readonly ICierreMensualGuard _cierreGuard = cierreGuard ?? new CierreMensualGuard(db);

    public async Task<List<PlantillaConteoItemDto>> PlantillaAsync(int hotelId, DateOnly? fecha = null, CancellationToken ct = default)
    {
        if (!currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var estado = await CargarEstadoProductosAsync(hotelId, fecha ?? DateOnly.FromDateTime(DateTime.UtcNow), ct);

        return estado.Values
            .OrderBy(e => e.Producto)
            .Select(e => new PlantillaConteoItemDto(
                e.ProductoId,
                e.Producto,
                e.Categoria,
                e.UnidadBase,
                Math.Round(e.ExistenciaSistemaBase, 4),
                Math.Round(e.StockMinimoBase, 4),
                Math.Round(e.UltimoPrecioBase, 4),
                Math.Round(Math.Max(0, e.ExistenciaSistemaBase) * e.UltimoPrecioBase, 4)))
            .ToList();
    }

    public async Task<List<ConteoInventarioResumenDto>> ListarAsync(FiltroConteos filtro, CancellationToken ct = default)
    {
        if (filtro.HotelId is { } hotelId && !currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var query = db.ConteosInventario
            .Include(c => c.Hotel)
            .Include(c => c.Detalles)
            .AsQueryable();

        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
        {
            var hoteles = currentUser.HotelesPermitidos;
            query = query.Where(c => hoteles.Contains(c.HotelId));
        }

        if (filtro.HotelId is not null) query = query.Where(c => c.HotelId == filtro.HotelId);
        if (filtro.Desde is not null) query = query.Where(c => c.Fecha >= filtro.Desde);
        if (filtro.Hasta is not null) query = query.Where(c => c.Fecha <= filtro.Hasta);

        var conteos = await query
            .OrderByDescending(c => c.Fecha)
            .ThenByDescending(c => c.Id)
            .Take(100)
            .ToListAsync(ct);

        return conteos.Select(MapearResumen).ToList();
    }

    public async Task<ConteoInventarioDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var conteo = await db.ConteosInventario
            .Include(c => c.Hotel)
            .Include(c => c.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.UnidadBase)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conteo is null) return null;

        if (!currentUser.PuedeAccederHotel(conteo.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        return Mapear(conteo);
    }

    public async Task<ConteoInventarioDto> CrearAsync(CrearConteoInventarioRequest req, CancellationToken ct = default)
    {
        if (!currentUser.PuedeAccederHotel(req.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(req.HotelId, req.Fecha, "crear conteos", ct);

        if (req.Detalles.Count == 0)
            throw new InvalidOperationException("El conteo debe incluir al menos un producto.");

        var ids = req.Detalles.Select(d => d.ProductoId).ToList();
        if (ids.Count != ids.Distinct().Count())
            throw new InvalidOperationException("No puedes repetir productos dentro del mismo conteo.");

        if (req.Detalles.Any(d => d.CantidadFisicaBase < 0))
            throw new InvalidOperationException("La cantidad fisica no puede ser negativa.");
        foreach (var detalle in req.Detalles)
            DecimalPrecision.ValidarEscalaOperativa(detalle.CantidadFisicaBase, "La cantidad fisica");

        var hotelExiste = await db.Hoteles.AnyAsync(h => h.Id == req.HotelId && h.Activo, ct);
        if (!hotelExiste)
            throw new InvalidOperationException("Hotel invalido o inactivo.");

        var estado = await CargarEstadoProductosAsync(req.HotelId, req.Fecha, ct);
        var productosInvalidos = ids.Where(id => !estado.ContainsKey(id)).ToList();
        if (productosInvalidos.Count > 0)
            throw new InvalidOperationException("El conteo incluye productos invalidos o inactivos.");

        var conteo = new ConteoInventario
        {
            Fecha = req.Fecha,
            HotelId = req.HotelId,
            Estado = EstadoConteoInventario.Registrado,
            Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones.Trim(),
        };

        foreach (var linea in req.Detalles.OrderBy(d => estado[d.ProductoId].Producto))
        {
            var producto = estado[linea.ProductoId];
            var diferencia = linea.CantidadFisicaBase - producto.ExistenciaSistemaBase;

            conteo.Detalles.Add(new ConteoInventarioDetalle
            {
                ProductoId = linea.ProductoId,
                CantidadSistemaBase = producto.ExistenciaSistemaBase,
                CantidadFisicaBase = linea.CantidadFisicaBase,
                DiferenciaBase = diferencia,
                ValorDiferenciaEstimado = Math.Round(diferencia * producto.UltimoPrecioBase, 4),
            });
        }

        db.ConteosInventario.Add(conteo);
        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Conteo creado",
            "ConteoInventario",
            conteo.Id,
            conteo.HotelId,
            $"Conteo fisico #{conteo.Id} creado",
            $"Fecha {conteo.Fecha:dd/MM/yyyy}; productos {conteo.Detalles.Count}; diferencias {conteo.Detalles.Count(d => d.DiferenciaBase != 0)}",
            ct);

        return (await ObtenerAsync(conteo.Id, ct))!;
    }

    public async Task<ConteoInventarioDto?> AplicarAjustesAsync(int id, CancellationToken ct = default)
    {
        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
            throw new UnauthorizedAccessException("Solo Admin o Gerencia pueden aplicar ajustes de conteo.");

        var conteo = await db.ConteosInventario
            .Include(c => c.Detalles)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conteo is null) return null;

        if (!currentUser.PuedeAccederHotel(conteo.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        if (conteo.Estado == EstadoConteoInventario.Anulado)
            throw new InvalidOperationException("No se puede ajustar un conteo anulado.");

        if (conteo.Estado == EstadoConteoInventario.Ajustado)
            throw new InvalidOperationException("Este conteo ya tiene ajustes aplicados.");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(conteo.HotelId, conteo.Fecha, "aplicar ajustes de conteo", ct);

        foreach (var detalle in conteo.Detalles.Where(d => d.DiferenciaBase != 0))
        {
            var movimiento = new MovimientoInventario
            {
                HotelId = conteo.HotelId,
                ProductoId = detalle.ProductoId,
                Tipo = TipoMovimiento.Ajuste,
                Fecha = conteo.Fecha,
                CantidadBase = detalle.DiferenciaBase,
                Referencia = $"Conteo fisico #{conteo.Id}",
            };
            db.Movimientos.Add(movimiento);
            detalle.MovimientoAjuste = movimiento;
        }

        conteo.Estado = EstadoConteoInventario.Ajustado;
        conteo.AjustesAplicadosEn = DateTime.UtcNow;
        conteo.AjustesAplicadosPor = currentUser.UserName ?? "sistema";

        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Ajustes de conteo aplicados",
            "ConteoInventario",
            conteo.Id,
            conteo.HotelId,
            $"Ajustes aplicados al conteo #{conteo.Id}",
            $"Movimientos creados {conteo.Detalles.Count(d => d.MovimientoAjusteId is not null)}",
            ct);
        return await ObtenerAsync(conteo.Id, ct);
    }

    private async Task<Dictionary<int, EstadoProductoConteo>> CargarEstadoProductosAsync(
        int hotelId, DateOnly hasta, CancellationToken ct)
    {
        var productos = await db.Productos
            .Include(p => p.UnidadBase)
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new EstadoProductoConteo(
                p.Id,
                p.Nombre,
                p.Categoria.ToString(),
                p.UnidadBase.Nombre,
                0,
                0,
                0))
            .ToDictionaryAsync(p => p.ProductoId, ct);

        var compras = await db.Detalles
            .Where(d => d.DocumentoCompra.HotelId == hotelId
                        && d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido
                        && d.DocumentoCompra.Fecha <= hasta)
            .GroupBy(d => d.ProductoId)
            .Select(g => new
            {
                ProductoId = g.Key,
                Cantidad = g.Sum(d => d.Cantidad * d.FactorABase),
            })
            .ToListAsync(ct);

        var movimientos = await db.Movimientos
            .Where(m => m.HotelId == hotelId && m.Fecha <= hasta)
            .GroupBy(m => new { m.ProductoId, m.Tipo })
            .Select(g => new
            {
                g.Key.ProductoId,
                g.Key.Tipo,
                Cantidad = g.Sum(m => m.CantidadBase),
            })
            .ToListAsync(ct);

        var stockMinimos = await db.StockMinimos
            .Where(s => s.HotelId == hotelId)
            .ToDictionaryAsync(s => s.ProductoId, s => s.CantidadMinimaBase, ct);

        var ultimosPrecios = await db.Detalles
            .Where(d => d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido
                        && d.DocumentoCompra.Fecha <= hasta
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

        var ultimoPrecioPorProducto = ultimosPrecios
            .GroupBy(d => d.ProductoId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(d => d.HotelId == hotelId)
                    .ThenByDescending(d => d.Fecha)
                    .ThenByDescending(d => d.Id)
                    .First()
                    .PrecioBase);

        foreach (var compra in compras)
            if (productos.TryGetValue(compra.ProductoId, out var p))
                productos[compra.ProductoId] = p with { ExistenciaSistemaBase = p.ExistenciaSistemaBase + compra.Cantidad };

        foreach (var movimiento in movimientos)
        {
            if (!productos.TryGetValue(movimiento.ProductoId, out var p)) continue;
            var efecto = movimiento.Tipo switch
            {
                TipoMovimiento.Entrada => movimiento.Cantidad,
                TipoMovimiento.Salida => -movimiento.Cantidad,
                TipoMovimiento.Merma => -movimiento.Cantidad,
                TipoMovimiento.Ajuste => movimiento.Cantidad,
                _ => 0,
            };
            productos[movimiento.ProductoId] = p with { ExistenciaSistemaBase = p.ExistenciaSistemaBase + efecto };
        }

        foreach (var producto in productos.Values.ToList())
        {
            productos[producto.ProductoId] = producto with
            {
                StockMinimoBase = stockMinimos.GetValueOrDefault(producto.ProductoId),
                UltimoPrecioBase = ultimoPrecioPorProducto.GetValueOrDefault(producto.ProductoId),
            };
        }

        return productos;
    }

    private static ConteoInventarioResumenDto MapearResumen(ConteoInventario c) => new(
        c.Id,
        c.Fecha,
        c.HotelId,
        c.Hotel.Nombre,
        c.Estado.ToString(),
        c.Detalles.Count,
        c.Detalles.Count(d => d.DiferenciaBase != 0),
        Math.Round(c.Detalles.Sum(d => Math.Abs(d.ValorDiferenciaEstimado)), 4),
        c.Observaciones,
        c.CreadoEn,
        c.CreadoPor,
        c.AjustesAplicadosEn,
        c.AjustesAplicadosPor);

    private static ConteoInventarioDto Mapear(ConteoInventario c)
    {
        var detalles = c.Detalles
            .OrderByDescending(d => Math.Abs(d.ValorDiferenciaEstimado))
            .ThenBy(d => d.Producto.Nombre)
            .Select(d => new ConteoInventarioDetalleDto(
                d.Id,
                d.ProductoId,
                d.Producto.Nombre,
                d.Producto.Categoria.ToString(),
                d.Producto.UnidadBase.Nombre,
                Math.Round(d.CantidadSistemaBase, 4),
                Math.Round(d.CantidadFisicaBase, 4),
                Math.Round(d.DiferenciaBase, 4),
                Math.Round(d.ValorDiferenciaEstimado, 4),
                d.MovimientoAjusteId))
            .ToList();

        return new ConteoInventarioDto(
            c.Id,
            c.Fecha,
            c.HotelId,
            c.Hotel.Nombre,
            c.Estado.ToString(),
            c.Observaciones,
            c.CreadoEn,
            c.CreadoPor,
            c.AjustesAplicadosEn,
            c.AjustesAplicadosPor,
            detalles.Count,
            detalles.Count(d => d.DiferenciaBase != 0),
            Math.Round(detalles.Sum(d => Math.Abs(d.ValorDiferenciaEstimado)), 4),
            detalles);
    }

    private sealed record EstadoProductoConteo(
        int ProductoId,
        string Producto,
        string Categoria,
        string UnidadBase,
        decimal ExistenciaSistemaBase,
        decimal StockMinimoBase,
        decimal UltimoPrecioBase);

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
