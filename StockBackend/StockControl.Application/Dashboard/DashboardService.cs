using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Dashboard;

/// <summary>
/// Consultas analíticas sobre las compras. Todos los precios se normalizan a la
/// unidad base del producto (precio ponderado = gasto / cantidad base), de modo
/// que compras en cajas, mallas o libras sean comparables entre sí.
/// </summary>
public class DashboardService(IApplicationDbContext db, ICurrentUser currentUser) : IDashboardService
{
    public async Task<ResumenMensualDto> ResumenMensualAsync(int anio, int mes, CancellationToken ct = default)
    {
        var inicio = new DateOnly(anio, mes, 1);
        var fin = inicio.AddMonths(1);
        var inicioAnterior = inicio.AddMonths(-1);

        var delMes = Detalles().Where(d => d.DocumentoCompra.Fecha >= inicio && d.DocumentoCompra.Fecha < fin);

        var gastoTotal = await delMes.SumAsync(d => (decimal?)(d.Cantidad * d.PrecioUnitario), ct) ?? 0m;

        var gastoAnterior = await Detalles()
            .Where(d => d.DocumentoCompra.Fecha >= inicioAnterior && d.DocumentoCompra.Fecha < inicio)
            .SumAsync(d => (decimal?)(d.Cantidad * d.PrecioUnitario), ct) ?? 0m;

        var documentos = await Documentos()
            .CountAsync(d => d.Fecha >= inicio && d.Fecha < fin, ct);

        var porHotelRaw = await delMes
            .GroupBy(d => new { d.DocumentoCompra.HotelId, d.DocumentoCompra.Hotel.Nombre })
            .Select(g => new { g.Key.HotelId, g.Key.Nombre, Gasto = g.Sum(d => d.Cantidad * d.PrecioUnitario) })
            .OrderByDescending(x => x.Gasto)
            .ToListAsync(ct);

        // Comensales y presupuestos del mes → costo por comensal y % de presupuesto usado.
        var comensales = await db.Comensales
            .Where(c => c.Anio == anio && c.Mes == mes)
            .ToDictionaryAsync(c => c.HotelId, c => c.NumeroComensales, ct);
        var presupuestos = (await db.Presupuestos
                .Where(p => p.Anio == anio && p.Mes == mes)
                .GroupBy(p => p.HotelId)
                .Select(g => new { HotelId = g.Key, Monto = g.Sum(p => p.Monto) })
                .ToListAsync(ct))
            .ToDictionary(x => x.HotelId, x => x.Monto);

        var porHotel = porHotelRaw.Select(x =>
        {
            int? numComensales = comensales.TryGetValue(x.HotelId, out var c) && c > 0 ? c : null;
            decimal? presupuesto = presupuestos.TryGetValue(x.HotelId, out var p) && p > 0 ? p : null;
            return new GastoPorHotelDto(
                x.HotelId, x.Nombre, x.Gasto,
                numComensales,
                numComensales is null ? null : Math.Round(x.Gasto / numComensales.Value, 2),
                presupuesto,
                presupuesto is null ? null : Math.Round(x.Gasto / presupuesto.Value * 100, 1));
        }).ToList();

        var porCategoria = await delMes
            .GroupBy(d => d.Producto.Categoria)
            .Select(g => new { g.Key, Gasto = g.Sum(d => d.Cantidad * d.PrecioUnitario) })
            .OrderByDescending(x => x.Gasto)
            .ToListAsync(ct);

        var variacion = gastoAnterior == 0 ? 0 : Math.Round((gastoTotal - gastoAnterior) / gastoAnterior * 100, 2);

        return new ResumenMensualDto(
            anio, mes, gastoTotal, gastoAnterior, variacion, documentos,
            porHotel,
            porCategoria.Select(x => new GastoPorCategoriaDto(x.Key.ToString(), x.Gasto)).ToList());
    }

    public async Task<List<TopProductoDto>> TopCompradosAsync(int meses, int top, CancellationToken ct = default)
    {
        var agregado = await AgregadoPorProducto(meses).OrderByDescending(x => x.CantidadBase).Take(top).ToListAsync(ct);
        return agregado.Select(MapearTop).ToList();
    }

    public async Task<List<TopProductoDto>> TopCarosAsync(int meses, int top, CancellationToken ct = default)
    {
        var agregado = await AgregadoPorProducto(meses)
            .OrderByDescending(x => x.Gasto / x.CantidadBase)
            .Take(top)
            .ToListAsync(ct);
        return agregado.Select(MapearTop).ToList();
    }

    public async Task<TendenciaPrecioDto?> TendenciaPrecioAsync(int productoId, int meses, CancellationToken ct = default)
    {
        var producto = await db.Productos.Include(p => p.UnidadBase)
            .FirstOrDefaultAsync(p => p.Id == productoId, ct);
        if (producto is null) return null;

        var desde = MesesAtras(meses);

        var puntos = await Detalles()
            .Where(d => d.ProductoId == productoId && d.DocumentoCompra.Fecha >= desde)
            .GroupBy(d => new { d.DocumentoCompra.Fecha.Year, d.DocumentoCompra.Fecha.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Gasto = g.Sum(d => d.Cantidad * d.PrecioUnitario),
                CantidadBase = g.Sum(d => d.Cantidad * d.FactorABase),
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        var serie = puntos
            .Select(p => new PuntoMensualDto(p.Year, p.Month, Math.Round(p.Gasto / p.CantidadBase, 2)))
            .ToList();

        return new TendenciaPrecioDto(producto.Id, producto.Nombre, producto.UnidadBase.Nombre, serie);
    }

    public async Task<List<ConsumoHotelSerieDto>> ConsumoPorHotelAsync(int meses, CancellationToken ct = default)
    {
        var desde = MesesAtras(meses);

        var filas = await Detalles()
            .Where(d => d.DocumentoCompra.Fecha >= desde)
            .GroupBy(d => new
            {
                d.DocumentoCompra.HotelId,
                Hotel = d.DocumentoCompra.Hotel.Nombre,
                d.DocumentoCompra.Fecha.Year,
                d.DocumentoCompra.Fecha.Month,
            })
            .Select(g => new
            {
                g.Key.HotelId,
                g.Key.Hotel,
                g.Key.Year,
                g.Key.Month,
                Gasto = g.Sum(d => d.Cantidad * d.PrecioUnitario),
            })
            .ToListAsync(ct);

        return filas
            .GroupBy(f => new { f.HotelId, f.Hotel })
            .Select(g => new ConsumoHotelSerieDto(
                g.Key.HotelId,
                g.Key.Hotel,
                g.OrderBy(x => x.Year).ThenBy(x => x.Month)
                    .Select(x => new PuntoMensualDto(x.Year, x.Month, x.Gasto)).ToList()))
            .OrderBy(s => s.Hotel)
            .ToList();
    }

    public async Task<List<AlertaPrecioDto>> AlertasPrecioAsync(decimal umbralPorcentaje, CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicioReciente = hoy.AddDays(-30);
        var inicioReferencia = inicioReciente.AddDays(-90);

        // Precio ponderado reciente (últimos 30 días) por producto.
        var recientes = await Detalles()
            .Where(d => d.DocumentoCompra.Fecha >= inicioReciente)
            .GroupBy(d => new { d.ProductoId, Producto = d.Producto.Nombre, Unidad = d.Producto.UnidadBase.Nombre })
            .Select(g => new
            {
                g.Key.ProductoId,
                g.Key.Producto,
                g.Key.Unidad,
                Precio = g.Sum(d => d.Cantidad * d.PrecioUnitario) / g.Sum(d => d.Cantidad * d.FactorABase),
                UltimaCompra = g.Max(d => d.DocumentoCompra.Fecha),
            })
            .ToListAsync(ct);

        if (recientes.Count == 0) return [];

        // Referencia: media móvil de los 90 días anteriores a la ventana reciente.
        var ids = recientes.Select(r => r.ProductoId).ToList();
        var referencias = await Detalles()
            .Where(d => ids.Contains(d.ProductoId)
                        && d.DocumentoCompra.Fecha >= inicioReferencia
                        && d.DocumentoCompra.Fecha < inicioReciente)
            .GroupBy(d => d.ProductoId)
            .Select(g => new
            {
                ProductoId = g.Key,
                Precio = g.Sum(d => d.Cantidad * d.PrecioUnitario) / g.Sum(d => d.Cantidad * d.FactorABase),
            })
            .ToDictionaryAsync(x => x.ProductoId, x => x.Precio, ct);

        var alertas = new List<AlertaPrecioDto>();
        foreach (var r in recientes)
        {
            if (!referencias.TryGetValue(r.ProductoId, out var referencia) || referencia <= 0) continue;

            var incremento = (r.Precio - referencia) / referencia * 100;
            if (incremento >= umbralPorcentaje)
                alertas.Add(new AlertaPrecioDto(
                    r.ProductoId, r.Producto, r.Unidad,
                    Math.Round(r.Precio, 2), Math.Round(referencia, 2),
                    Math.Round(incremento, 1), r.UltimaCompra));
        }

        return alertas.OrderByDescending(a => a.IncrementoPorcentaje).ToList();
    }

    public async Task<DashboardGerencialDto> GerencialAsync(int anio, int mes, CancellationToken ct = default)
    {
        var inicio = new DateOnly(anio, mes, 1);
        var fin = inicio.AddMonths(1);
        var corte = fin.AddDays(-1);

        var hoteles = await HotelesPermitidos()
            .Select(h => new { h.Id, h.Nombre })
            .ToListAsync(ct);
        var hotelIds = hoteles.Select(h => h.Id).ToHashSet();

        if (hotelIds.Count == 0)
        {
            return new DashboardGerencialDto(
                anio, mes, 0, 0, 0, 0, 0, 0, 0,
                currentUser.EsAdmin || currentUser.EsGerencia,
                0, 0, 0, [], [], []);
        }

        var productos = await db.Productos
            .Include(p => p.UnidadBase)
            .Where(p => p.Activo)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                Categoria = p.Categoria.ToString(),
                UnidadBase = p.UnidadBase.Nombre,
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var detalles = await db.Detalles
            .Where(d => d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido
                        && hotelIds.Contains(d.DocumentoCompra.HotelId)
                        && d.DocumentoCompra.Fecha < fin)
            .Select(d => new
            {
                d.Id,
                d.ProductoId,
                d.DocumentoCompra.HotelId,
                d.DocumentoCompra.Fecha,
                CantidadBase = d.Cantidad * d.FactorABase,
                Total = d.Cantidad * d.PrecioUnitario,
                PrecioBase = d.FactorABase == 0 ? 0 : d.PrecioUnitario / d.FactorABase,
            })
            .ToListAsync(ct);

        var movimientos = await db.Movimientos
            .Where(m => hotelIds.Contains(m.HotelId) && m.Fecha < fin)
            .Select(m => new
            {
                m.ProductoId,
                m.HotelId,
                m.Tipo,
                m.Fecha,
                m.CantidadBase,
            })
            .ToListAsync(ct);

        var ultimoPrecio = detalles
            .Where(d => d.PrecioBase > 0)
            .GroupBy(d => d.ProductoId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(d => d.Fecha).ThenByDescending(d => d.Id).First().PrecioBase);

        var existenciaPorHotelProducto = new Dictionary<(int HotelId, int ProductoId), decimal>();
        foreach (var compra in detalles)
            Sumar(existenciaPorHotelProducto, (compra.HotelId, compra.ProductoId), compra.CantidadBase);

        foreach (var movimiento in movimientos)
        {
            var efecto = movimiento.Tipo switch
            {
                TipoMovimiento.Entrada => movimiento.CantidadBase,
                TipoMovimiento.Salida => -movimiento.CantidadBase,
                TipoMovimiento.Merma => -movimiento.CantidadBase,
                TipoMovimiento.Ajuste => movimiento.CantidadBase,
                _ => 0,
            };
            Sumar(existenciaPorHotelProducto, (movimiento.HotelId, movimiento.ProductoId), efecto);
        }

        var valorInventario = existenciaPorHotelProducto.Sum(x =>
            Math.Max(0, x.Value) * ultimoPrecio.GetValueOrDefault(x.Key.ProductoId));

        var minimos = await db.StockMinimos
            .Where(s => hotelIds.Contains(s.HotelId))
            .Select(s => new { s.HotelId, s.ProductoId, s.CantidadMinimaBase })
            .ToListAsync(ct);

        var hotelNombres = hoteles.ToDictionary(h => h.Id, h => h.Nombre);
        var stockCritico = minimos
            .Select(s =>
            {
                var existencia = existenciaPorHotelProducto.GetValueOrDefault((s.HotelId, s.ProductoId));
                var faltante = Math.Max(0, s.CantidadMinimaBase - existencia);
                var producto = productos.GetValueOrDefault(s.ProductoId);
                var precio = ultimoPrecio.GetValueOrDefault(s.ProductoId);
                return new
                {
                    s.HotelId,
                    Hotel = hotelNombres.GetValueOrDefault(s.HotelId) ?? "",
                    s.ProductoId,
                    Producto = producto?.Nombre ?? "",
                    Categoria = producto?.Categoria ?? "",
                    UnidadBase = producto?.UnidadBase ?? "",
                    Existencia = existencia,
                    StockMinimo = s.CantidadMinimaBase,
                    Faltante = faltante,
                    ValorFaltante = faltante * precio,
                    Estado = existencia < 0 ? "Negativo" : existencia == 0 ? "SinStock" : "BajoMinimo",
                };
            })
            .Where(s => s.Faltante > 0)
            .OrderByDescending(s => s.ValorFaltante)
            .ThenByDescending(s => s.Faltante)
            .Take(10)
            .Select(s => new StockCriticoDto(
                s.HotelId,
                s.Hotel,
                s.ProductoId,
                s.Producto,
                s.Categoria,
                s.UnidadBase,
                Math.Round(s.Existencia, 2),
                Math.Round(s.StockMinimo, 2),
                Math.Round(s.Faltante, 2),
                Math.Round(s.ValorFaltante, 2),
                s.Estado))
            .ToList();

        var mermasMes = movimientos
            .Where(m => m.Tipo == TipoMovimiento.Merma && m.Fecha >= inicio && m.Fecha < fin)
            .ToList();
        var ajustesMes = movimientos
            .Where(m => m.Tipo == TipoMovimiento.Ajuste && m.Fecha >= inicio && m.Fecha < fin)
            .ToList();

        var topMermas = mermasMes
            .GroupBy(m => m.ProductoId)
            .Select(g =>
            {
                var producto = productos.GetValueOrDefault(g.Key);
                var cantidad = g.Sum(m => m.CantidadBase);
                var precio = ultimoPrecio.GetValueOrDefault(g.Key);
                return new MermaProductoDto(
                    g.Key,
                    producto?.Nombre ?? "",
                    producto?.Categoria ?? "",
                    producto?.UnidadBase ?? "",
                    Math.Round(cantidad, 2),
                    Math.Round(cantidad * precio, 2));
            })
            .OrderByDescending(m => m.ValorEstimado)
            .ThenByDescending(m => m.CantidadBase)
            .Take(8)
            .ToList();

        var valorMermas = mermasMes.Sum(m => m.CantidadBase * ultimoPrecio.GetValueOrDefault(m.ProductoId));
        var valorAjustes = ajustesMes.Sum(m => Math.Abs(m.CantidadBase) * ultimoPrecio.GetValueOrDefault(m.ProductoId));

        var incluyeFinanzas = currentUser.EsAdmin || currentUser.EsGerencia;
        decimal? saldoCxp = null;
        decimal? saldoVencido = null;
        int? documentosVencidos = null;
        var topProveedores = new List<TopProveedorSaldoDto>();

        if (incluyeFinanzas)
        {
            var documentos = await db.Documentos
                .Include(d => d.Proveedor)
                .Include(d => d.Detalles)
                .Include(d => d.Pagos)
                .Where(d => d.Estado == EstadoDocumentoCompra.Recibido
                            && hotelIds.Contains(d.HotelId)
                            && (d.Observaciones ?? "") != DocumentoCompra.ObservacionImportadoExcel
                            && d.Proveedor.Nombre != Proveedor.NombreProveedorImportacionExcel
                            && d.Fecha < fin)
                .ToListAsync(ct);

            var cuentas = documentos
                .Select(d =>
                {
                    var bruto = d.Total;
                    var neto = Math.Max(0, bruto - d.Retencion);
                    var pagado = d.Pagos.Where(p => p.Fecha < fin).Sum(p => p.Monto);
                    var saldo = Math.Max(0, neto - pagado);
                    var vencido = saldo > 0 && d.Fecha.AddDays(d.Proveedor.DiasCredito) < corte;
                    return new
                    {
                        d.ProveedorId,
                        Proveedor = d.Proveedor.Nombre,
                        Saldo = saldo,
                        SaldoVencido = vencido ? saldo : 0,
                        Vencido = vencido,
                    };
                })
                .Where(c => c.Saldo > 0)
                .ToList();

            saldoCxp = Math.Round(cuentas.Sum(c => c.Saldo), 2);
            saldoVencido = Math.Round(cuentas.Sum(c => c.SaldoVencido), 2);
            documentosVencidos = cuentas.Count(c => c.Vencido);

            topProveedores = cuentas
                .GroupBy(c => new { c.ProveedorId, c.Proveedor })
                .Select(g => new TopProveedorSaldoDto(
                    g.Key.ProveedorId,
                    g.Key.Proveedor,
                    g.Count(),
                    Math.Round(g.Sum(c => c.Saldo), 2),
                    Math.Round(g.Sum(c => c.SaldoVencido), 2)))
                .OrderByDescending(p => p.Saldo)
                .Take(8)
                .ToList();
        }

        return new DashboardGerencialDto(
            anio,
            mes,
            Math.Round(valorInventario, 2),
            stockCritico.Count,
            Math.Round(stockCritico.Sum(s => s.ValorFaltanteEstimado), 2),
            Math.Round(valorMermas, 2),
            mermasMes.Count,
            Math.Round(valorAjustes, 2),
            ajustesMes.Count,
            incluyeFinanzas,
            saldoCxp,
            saldoVencido,
            documentosVencidos,
            topProveedores,
            topMermas,
            stockCritico);
    }

    // --- Auxiliares ---

    private IQueryable<DetalleCompra> Detalles()
    {
        var query = db.Detalles.Where(d => d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido);
        if (currentUser.EsAdmin || currentUser.EsGerencia) return query;
        var hoteles = currentUser.HotelesPermitidos;
        return query.Where(d => hoteles.Contains(d.DocumentoCompra.HotelId));
    }

    private IQueryable<DocumentoCompra> Documentos()
    {
        var query = db.Documentos.Where(d => d.Estado == EstadoDocumentoCompra.Recibido);
        if (currentUser.EsAdmin || currentUser.EsGerencia) return query;
        var hoteles = currentUser.HotelesPermitidos;
        return query.Where(d => hoteles.Contains(d.HotelId));
    }

    /// <summary>Clase con member-init (no record posicional) para que EF Core traduzca la proyección agrupada.</summary>
    private IQueryable<Hotel> HotelesPermitidos()
    {
        var query = db.Hoteles.Where(h => h.Activo);
        if (currentUser.EsAdmin || currentUser.EsGerencia) return query;
        var hoteles = currentUser.HotelesPermitidos;
        return query.Where(h => hoteles.Contains(h.Id));
    }

    private static void Sumar<TKey>(Dictionary<TKey, decimal> diccionario, TKey llave, decimal valor)
        where TKey : notnull
    {
        diccionario[llave] = diccionario.GetValueOrDefault(llave) + valor;
    }

    private sealed class AgregadoProducto
    {
        public int ProductoId { get; init; }
        public string Producto { get; init; } = null!;
        public Domain.Enums.CategoriaProducto Categoria { get; init; }
        public string Unidad { get; init; } = null!;
        public decimal CantidadBase { get; init; }
        public decimal Gasto { get; init; }
    }

    private IQueryable<AgregadoProducto> AgregadoPorProducto(int meses)
    {
        var desde = MesesAtras(meses);
        return Detalles()
            .Where(d => d.DocumentoCompra.Fecha >= desde)
            .GroupBy(d => new
            {
                d.ProductoId,
                Producto = d.Producto.Nombre,
                d.Producto.Categoria,
                Unidad = d.Producto.UnidadBase.Nombre,
            })
            .Select(g => new AgregadoProducto
            {
                ProductoId = g.Key.ProductoId,
                Producto = g.Key.Producto,
                Categoria = g.Key.Categoria,
                Unidad = g.Key.Unidad,
                CantidadBase = g.Sum(d => d.Cantidad * d.FactorABase),
                Gasto = g.Sum(d => d.Cantidad * d.PrecioUnitario),
            });
    }

    private static TopProductoDto MapearTop(AgregadoProducto a) => new(
        a.ProductoId, a.Producto, a.Categoria.ToString(), a.Unidad,
        Math.Round(a.CantidadBase, 2), Math.Round(a.Gasto, 2),
        a.CantidadBase == 0 ? 0 : Math.Round(a.Gasto / a.CantidadBase, 2));

    private static DateOnly MesesAtras(int meses)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        // Desde el día 1 para incluir meses completos.
        return new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-(meses - 1));
    }
}
