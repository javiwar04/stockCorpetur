using Microsoft.EntityFrameworkCore;
using StockControl.Application.Auditoria;
using StockControl.Application.Cierres;
using StockControl.Application.Common;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Inventario;

public interface IInventarioService
{
    Task<List<ExistenciaDto>> ExistenciasAsync(int hotelId, CancellationToken ct = default);
    Task<KardexDto> KardexAsync(FiltroKardex filtro, CancellationToken ct = default);
    Task<List<MovimientoDto>> ListarMovimientosAsync(FiltroMovimientos filtro, CancellationToken ct = default);
    Task<MovimientoDto> RegistrarMovimientoAsync(CrearMovimientoRequest req, CancellationToken ct = default);
    Task<bool> EliminarMovimientoAsync(int id, CancellationToken ct = default);
    Task<List<StockMinimoDto>> ListarStockMinimoAsync(int hotelId, CancellationToken ct = default);
    Task<StockMinimoDto> GuardarStockMinimoAsync(GuardarStockMinimoRequest req, CancellationToken ct = default);
    Task<bool> EliminarStockMinimoAsync(int hotelId, int productoId, CancellationToken ct = default);
    Task<List<AlertaStockDto>> AlertasStockAsync(int? hotelId = null, CancellationToken ct = default);
    Task<List<SugerenciaCompraDto>> SugerenciasCompraAsync(int hotelId, CancellationToken ct = default);
}

/// <summary>
/// Inventario por hotel. Las compras son entradas implícitas; los mínimos son umbrales por hotel/producto.
/// </summary>
public class InventarioService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICierreMensualGuard? cierreGuard = null,
    IAuditoriaService? auditoria = null) : IInventarioService
{
    private readonly ICierreMensualGuard _cierreGuard = cierreGuard ?? new CierreMensualGuard(db);

    public async Task<List<ExistenciaDto>> ExistenciasAsync(int hotelId, CancellationToken ct = default)
    {
        if (!currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var comprado = await db.Detalles
            .Where(d => d.DocumentoCompra.HotelId == hotelId && d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido)
            .GroupBy(d => d.ProductoId)
            .Select(g => new { ProductoId = g.Key, Total = g.Sum(d => d.Cantidad * d.FactorABase) })
            .ToDictionaryAsync(x => x.ProductoId, x => x.Total, ct);

        var movimientos = await db.Movimientos
            .Where(m => m.HotelId == hotelId)
            .GroupBy(m => new { m.ProductoId, m.Tipo })
            .Select(g => new { g.Key.ProductoId, g.Key.Tipo, Total = g.Sum(m => m.CantidadBase) })
            .ToListAsync(ct);

        var minimos = await db.StockMinimos
            .Where(s => s.HotelId == hotelId)
            .ToDictionaryAsync(s => s.ProductoId, s => s.CantidadMinimaBase, ct);

        decimal Mov(int productoId, TipoMovimiento tipo) =>
            movimientos.FirstOrDefault(m => m.ProductoId == productoId && m.Tipo == tipo)?.Total ?? 0m;

        var productos = await db.Productos
            .Include(p => p.UnidadBase)
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .ToListAsync(ct);

        return productos
            .Select(p =>
            {
                var compras = comprado.GetValueOrDefault(p.Id);
                var entradas = Mov(p.Id, TipoMovimiento.Entrada);
                var salidas = Mov(p.Id, TipoMovimiento.Salida);
                var mermas = Mov(p.Id, TipoMovimiento.Merma);
                var ajustes = Mov(p.Id, TipoMovimiento.Ajuste);

                var existencia = compras + entradas - salidas - mermas + ajustes;
                var stockMinimo = minimos.GetValueOrDefault(p.Id);
                var faltante = stockMinimo > existencia ? stockMinimo - existencia : 0m;

                return new ExistenciaDto(
                    p.Id,
                    p.Nombre,
                    p.Categoria.ToString(),
                    p.UnidadBase.Nombre,
                    Math.Round(compras + entradas, 4),
                    Math.Round(salidas, 4),
                    Math.Round(mermas, 4),
                    Math.Round(ajustes, 4),
                    Math.Round(existencia, 4),
                    Math.Round(stockMinimo, 4),
                    Math.Round(faltante, 4),
                    EstadoStock(existencia, stockMinimo));
            })
            .Where(e => e.Comprado != 0 || e.Salidas != 0 || e.Mermas != 0 || e.Ajustes != 0 || e.StockMinimo != 0)
            .ToList();
    }

    public async Task<KardexDto> KardexAsync(FiltroKardex filtro, CancellationToken ct = default)
    {
        if (!currentUser.PuedeAccederHotel(filtro.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        if (filtro.Desde is not null && filtro.Hasta is not null && filtro.Desde > filtro.Hasta)
            throw new InvalidOperationException("La fecha inicial no puede ser mayor a la fecha final.");

        var hotel = await db.Hoteles
            .Where(h => h.Id == filtro.HotelId)
            .Select(h => new { h.Id, h.Nombre })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Hotel no encontrado.");

        var producto = await db.Productos
            .Include(p => p.UnidadBase)
            .FirstOrDefaultAsync(p => p.Id == filtro.ProductoId, ct)
            ?? throw new InvalidOperationException("Producto no encontrado.");

        var comprasQuery = db.Detalles
            .Include(d => d.DocumentoCompra).ThenInclude(d => d.Proveedor)
            .Where(d => d.ProductoId == filtro.ProductoId
                        && d.DocumentoCompra.HotelId == filtro.HotelId
                        && d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido);

        var movimientosQuery = db.Movimientos
            .Where(m => m.ProductoId == filtro.ProductoId && m.HotelId == filtro.HotelId);

        if (filtro.Hasta is not null)
        {
            comprasQuery = comprasQuery.Where(d => d.DocumentoCompra.Fecha <= filtro.Hasta);
            movimientosQuery = movimientosQuery.Where(m => m.Fecha <= filtro.Hasta);
        }

        var compras = await comprasQuery.ToListAsync(ct);
        var movimientos = await movimientosQuery.ToListAsync(ct);

        var lineas = compras
            .Select(d => new KardexLinea(
                $"C-{d.Id}",
                d.DocumentoCompra.Fecha,
                0,
                "Compra",
                $"Compra {d.DocumentoCompra.NumeroDocumento}",
                d.CantidadBase,
                0m,
                0m,
                d.FactorABase == 0 ? null : Math.Round(d.PrecioPorUnidadBase, 4),
                Math.Round(d.Total, 4),
                d.DocumentoCompra.NumeroDocumento,
                d.DocumentoCompra.Proveedor.Nombre,
                null))
            .Concat(movimientos.Select(m =>
            {
                var entrada = m.Tipo == TipoMovimiento.Entrada ? m.CantidadBase : 0m;
                var salida = m.Tipo is TipoMovimiento.Salida or TipoMovimiento.Merma ? m.CantidadBase : 0m;
                var ajuste = m.Tipo == TipoMovimiento.Ajuste ? m.CantidadBase : 0m;
                return new KardexLinea(
                    $"M-{m.Id}",
                    m.Fecha,
                    1,
                    m.Tipo.ToString(),
                    string.IsNullOrWhiteSpace(m.Referencia) ? m.Tipo.ToString() : m.Referencia,
                    entrada,
                    salida,
                    ajuste,
                    null,
                    null,
                    null,
                    null,
                    m.CreadoPor);
            }))
            .OrderBy(l => l.Fecha)
            .ThenBy(l => l.Orden)
            .ThenBy(l => l.Id)
            .ToList();

        var saldoInicial = filtro.Desde is null
            ? 0m
            : lineas.Where(l => l.Fecha < filtro.Desde).Sum(l => l.Efecto);

        var lineasPeriodo = lineas
            .Where(l => filtro.Desde is null || l.Fecha >= filtro.Desde)
            .ToList();

        var saldo = saldoInicial;
        var kardexMovimientos = new List<KardexMovimientoDto>();
        foreach (var linea in lineasPeriodo)
        {
            saldo += linea.Efecto;
            kardexMovimientos.Add(new KardexMovimientoDto(
                linea.Id,
                linea.Fecha,
                linea.Tipo,
                linea.Referencia,
                Math.Round(linea.Entrada, 4),
                Math.Round(linea.Salida, 4),
                Math.Round(linea.Ajuste, 4),
                Math.Round(saldo, 4),
                linea.CostoUnitario,
                linea.CostoTotal,
                linea.Documento,
                linea.Proveedor,
                linea.CreadoPor));
        }

        return new KardexDto(
            hotel.Id,
            hotel.Nombre,
            producto.Id,
            producto.Nombre,
            producto.UnidadBase.Nombre,
            filtro.Desde,
            filtro.Hasta,
            Math.Round(saldoInicial, 4),
            Math.Round(lineasPeriodo.Sum(l => l.Entrada), 4),
            Math.Round(lineasPeriodo.Sum(l => l.Salida), 4),
            Math.Round(lineasPeriodo.Sum(l => l.Ajuste), 4),
            Math.Round(saldo, 4),
            kardexMovimientos);
    }

    public async Task<List<MovimientoDto>> ListarMovimientosAsync(FiltroMovimientos filtro, CancellationToken ct = default)
    {
        if (filtro.HotelId is { } hotelId && !currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var query = db.Movimientos
            .Include(m => m.Hotel)
            .Include(m => m.Producto).ThenInclude(p => p.UnidadBase)
            .AsQueryable();

        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
        {
            var hoteles = currentUser.HotelesPermitidos;
            query = query.Where(m => hoteles.Contains(m.HotelId));
        }

        if (filtro.HotelId is not null) query = query.Where(m => m.HotelId == filtro.HotelId);
        if (filtro.ProductoId is not null) query = query.Where(m => m.ProductoId == filtro.ProductoId);
        if (filtro.Desde is not null) query = query.Where(m => m.Fecha >= filtro.Desde);
        if (filtro.Hasta is not null) query = query.Where(m => m.Fecha <= filtro.Hasta);

        var movimientos = await query.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Take(200).ToListAsync(ct);

        return movimientos.Select(Mapear).ToList();
    }

    public async Task<MovimientoDto> RegistrarMovimientoAsync(CrearMovimientoRequest req, CancellationToken ct = default)
    {
        if (!currentUser.PuedeAccederHotel(req.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(req.HotelId, req.Fecha, "registrar movimientos", ct);

        if (!Enum.TryParse<TipoMovimiento>(req.Tipo, ignoreCase: true, out var tipo))
            throw new InvalidOperationException($"Tipo de movimiento inválido: {req.Tipo}");

        if (tipo == TipoMovimiento.Ajuste)
        {
            if (req.Cantidad == 0)
                throw new InvalidOperationException("El ajuste no puede ser cero.");
        }
        else if (req.Cantidad <= 0)
        {
            throw new InvalidOperationException("La cantidad debe ser mayor a cero.");
        }
        DecimalPrecision.ValidarEscalaOperativa(req.Cantidad, "La cantidad");

        var conversion = await db.Conversiones.FirstOrDefaultAsync(
            c => c.ProductoId == req.ProductoId && c.UnidadId == req.UnidadId, ct)
            ?? throw new InvalidOperationException("No existe conversión configurada para ese producto y unidad.");

        var cantidadBase = req.Cantidad * conversion.FactorABase;
        DecimalPrecision.ValidarEscalaOperativa(cantidadBase, "La cantidad base convertida");

        var movimiento = new MovimientoInventario
        {
            Tipo = tipo,
            Fecha = req.Fecha,
            HotelId = req.HotelId,
            ProductoId = req.ProductoId,
            CantidadBase = cantidadBase,
            Referencia = req.Referencia,
        };

        db.Movimientos.Add(movimiento);
        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Movimiento registrado",
            "MovimientoInventario",
            movimiento.Id,
            movimiento.HotelId,
            $"{movimiento.Tipo} de inventario registrado",
            $"ProductoId {movimiento.ProductoId}; cantidad base {movimiento.CantidadBase:N4}; fecha {movimiento.Fecha:dd/MM/yyyy}",
            ct);

        var completo = await db.Movimientos
            .Include(m => m.Hotel)
            .Include(m => m.Producto).ThenInclude(p => p.UnidadBase)
            .FirstAsync(m => m.Id == movimiento.Id, ct);
        return Mapear(completo);
    }

    public async Task<bool> EliminarMovimientoAsync(int id, CancellationToken ct = default)
    {
        var movimiento = await db.Movimientos.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (movimiento is null) return false;

        if (!currentUser.PuedeAccederHotel(movimiento.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        await _cierreGuard.AsegurarPeriodoAbiertoAsync(movimiento.HotelId, movimiento.Fecha, "eliminar movimientos", ct);

        var hotelId = movimiento.HotelId;
        var tipo = movimiento.Tipo;
        var productoId = movimiento.ProductoId;
        var cantidad = movimiento.CantidadBase;
        var fecha = movimiento.Fecha;
        db.Movimientos.Remove(movimiento);
        await db.SaveChangesAsync(ct);
        await AuditarAsync(
            "Movimiento eliminado",
            "MovimientoInventario",
            id,
            hotelId,
            $"{tipo} de inventario eliminado",
            $"ProductoId {productoId}; cantidad base {cantidad:N4}; fecha {fecha:dd/MM/yyyy}",
            ct);
        return true;
    }

    public async Task<List<StockMinimoDto>> ListarStockMinimoAsync(int hotelId, CancellationToken ct = default)
    {
        if (!currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        return await db.StockMinimos
            .Include(s => s.Producto).ThenInclude(p => p.UnidadBase)
            .Where(s => s.HotelId == hotelId)
            .OrderBy(s => s.Producto.Nombre)
            .Select(s => new StockMinimoDto(
                s.HotelId,
                s.ProductoId,
                s.Producto.Nombre,
                s.Producto.UnidadBase.Nombre,
                Math.Round(s.CantidadMinimaBase, 4)))
            .ToListAsync(ct);
    }

    public async Task<StockMinimoDto> GuardarStockMinimoAsync(GuardarStockMinimoRequest req, CancellationToken ct = default)
    {
        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
            throw new UnauthorizedAccessException("Solo Admin o Gerencia pueden configurar stock mínimo.");

        if (!currentUser.PuedeAccederHotel(req.HotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        if (req.CantidadMinimaBase <= 0)
            throw new InvalidOperationException("El stock mínimo debe ser mayor a cero.");

        DecimalPrecision.ValidarEscalaOperativa(req.CantidadMinimaBase, "El stock minimo");

        var producto = await db.Productos
            .Include(p => p.UnidadBase)
            .FirstOrDefaultAsync(p => p.Id == req.ProductoId && p.Activo, ct)
            ?? throw new InvalidOperationException("Producto inválido o inactivo.");

        var hotelExiste = await db.Hoteles.AnyAsync(h => h.Id == req.HotelId && h.Activo, ct);
        if (!hotelExiste)
            throw new InvalidOperationException("Hotel inválido o inactivo.");

        var stock = await db.StockMinimos.FirstOrDefaultAsync(
            s => s.HotelId == req.HotelId && s.ProductoId == req.ProductoId, ct);

        if (stock is null)
        {
            stock = new StockMinimo
            {
                HotelId = req.HotelId,
                ProductoId = req.ProductoId,
                CantidadMinimaBase = req.CantidadMinimaBase,
            };
            db.StockMinimos.Add(stock);
        }
        else
        {
            stock.CantidadMinimaBase = req.CantidadMinimaBase;
        }

        await db.SaveChangesAsync(ct);

        return new StockMinimoDto(
            req.HotelId,
            req.ProductoId,
            producto.Nombre,
            producto.UnidadBase.Nombre,
            Math.Round(stock.CantidadMinimaBase, 4));
    }

    public async Task<bool> EliminarStockMinimoAsync(int hotelId, int productoId, CancellationToken ct = default)
    {
        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
            throw new UnauthorizedAccessException("Solo Admin o Gerencia pueden configurar stock mínimo.");

        if (!currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var stock = await db.StockMinimos.FirstOrDefaultAsync(s => s.HotelId == hotelId && s.ProductoId == productoId, ct);
        if (stock is null) return false;

        db.StockMinimos.Remove(stock);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<AlertaStockDto>> AlertasStockAsync(int? hotelId = null, CancellationToken ct = default)
    {
        var hotelesQuery = db.Hoteles.Where(h => h.Activo);
        if (hotelId is not null)
        {
            if (!currentUser.PuedeAccederHotel(hotelId.Value))
                throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

            hotelesQuery = hotelesQuery.Where(h => h.Id == hotelId.Value);
        }
        else if (!currentUser.EsAdmin && !currentUser.EsGerencia)
        {
            var hotelesPermitidos = currentUser.HotelesPermitidos;
            hotelesQuery = hotelesQuery.Where(h => hotelesPermitidos.Contains(h.Id));
        }

        var hoteles = await hotelesQuery
            .OrderBy(h => h.Nombre)
            .Select(h => new { h.Id, h.Nombre })
            .ToListAsync(ct);

        var alertas = new List<AlertaStockDto>();
        foreach (var hotel in hoteles)
        {
            var existencias = await ExistenciasAsync(hotel.Id, ct);
            alertas.AddRange(existencias
                .Where(e => e.EstadoStock == "Negativo" || (e.StockMinimo > 0 && e.Existencia < e.StockMinimo))
                .Select(e => new AlertaStockDto(
                    hotel.Id,
                    hotel.Nombre,
                    e.ProductoId,
                    e.Producto,
                    e.Categoria,
                    e.UnidadBase,
                    e.Existencia,
                    e.StockMinimo,
                    e.Faltante,
                    e.EstadoStock)));
        }

        return alertas
            .OrderBy(a => PrioridadEstado(a.EstadoStock))
            .ThenByDescending(a => a.Faltante)
            .ThenBy(a => a.Hotel)
            .ThenBy(a => a.Producto)
            .ToList();
    }

    public async Task<List<SugerenciaCompraDto>> SugerenciasCompraAsync(int hotelId, CancellationToken ct = default)
    {
        if (!currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var hotel = await db.Hoteles
            .Where(h => h.Id == hotelId && h.Activo)
            .Select(h => new { h.Id, h.Nombre })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Hotel inválido o inactivo.");

        var faltantes = (await ExistenciasAsync(hotelId, ct))
            .Where(e => e.StockMinimo > 0 && e.Existencia < e.StockMinimo)
            .OrderBy(e => PrioridadEstado(e.EstadoStock))
            .ThenByDescending(e => e.Faltante)
            .ThenBy(e => e.Producto)
            .ToList();

        if (faltantes.Count == 0) return [];

        var productos = faltantes.Select(e => e.ProductoId).ToHashSet();
        var detalles = await db.Detalles
            .Include(d => d.DocumentoCompra).ThenInclude(d => d.Proveedor)
            .Where(d => productos.Contains(d.ProductoId)
                        && d.FactorABase > 0
                        && d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido)
            .ToListAsync(ct);

        var ultimoPorProducto = detalles
            .GroupBy(d => d.ProductoId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(d => d.DocumentoCompra.HotelId == hotelId)
                    .ThenByDescending(d => d.DocumentoCompra.Fecha)
                    .ThenByDescending(d => d.Id)
                    .First());

        return faltantes.Select(e =>
        {
            if (!ultimoPorProducto.TryGetValue(e.ProductoId, out var ultimo))
            {
                return new SugerenciaCompraDto(
                    hotel.Id, hotel.Nombre, e.ProductoId, e.Producto, e.Categoria, e.UnidadBase,
                    e.Faltante, e.Existencia, e.StockMinimo,
                    null, null, null, null, null);
            }

            var precioBase = ultimo.PrecioUnitario / ultimo.FactorABase;
            return new SugerenciaCompraDto(
                hotel.Id,
                hotel.Nombre,
                e.ProductoId,
                e.Producto,
                e.Categoria,
                e.UnidadBase,
                e.Faltante,
                e.Existencia,
                e.StockMinimo,
                Math.Round(precioBase, 4),
                ultimo.DocumentoCompra.ProveedorId,
                ultimo.DocumentoCompra.Proveedor.Nombre,
                ultimo.DocumentoCompra.Fecha,
                Math.Round(e.Faltante * precioBase, 4));
        }).ToList();
    }

    private static MovimientoDto Mapear(MovimientoInventario m) => new(
        m.Id, m.Tipo.ToString(), m.Fecha, m.HotelId, m.Hotel.Nombre,
        m.ProductoId, m.Producto.Nombre, m.Producto.UnidadBase.Nombre,
        m.CantidadBase, m.Referencia, m.CreadoPor);

    private static string EstadoStock(decimal existencia, decimal stockMinimo)
    {
        if (existencia < 0) return "Negativo";
        if (existencia == 0) return "SinStock";
        if (stockMinimo > 0 && existencia < stockMinimo) return "BajoMinimo";
        if (stockMinimo <= 0) return "SinConfigurar";
        return "Ok";
    }

    private static int PrioridadEstado(string estado) => estado switch
    {
        "Negativo" => 0,
        "SinStock" => 1,
        "BajoMinimo" => 2,
        _ => 3,
    };

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

    private sealed record KardexLinea(
        string Id,
        DateOnly Fecha,
        int Orden,
        string Tipo,
        string Referencia,
        decimal Entrada,
        decimal Salida,
        decimal Ajuste,
        decimal? CostoUnitario,
        decimal? CostoTotal,
        string? Documento,
        string? Proveedor,
        string? CreadoPor)
    {
        public decimal Efecto => Entrada - Salida + Ajuste;
    }
}
