using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Alertas;

public class AlertaService(IApplicationDbContext db, ICurrentUser currentUser) : IAlertaService
{
    private const decimal UmbralDiferenciaConteo = 100m;

    public async Task<AlertasResultadoDto> ListarAsync(CancellationToken ct = default)
    {
        var hoteles = await CargarHotelesPermitidosAsync(ct);
        var hotelIds = hoteles.Select(h => h.Id).ToHashSet();
        var alertas = new List<AlertaDto>();

        if (hotelIds.Count > 0)
        {
            alertas.AddRange(await AlertasStockAsync(hoteles, hotelIds, ct));
            alertas.AddRange(await AlertasConteosAsync(hotelIds, ct));

            if (currentUser.EsAdmin || currentUser.EsGerencia)
            {
                alertas.AddRange(await AlertasCuentasVencidasAsync(hotelIds, ct));
                alertas.AddRange(await AlertasCierresPendientesAsync(hoteles, ct));
            }
        }

        var ordenadas = alertas
            .OrderBy(a => Prioridad(a.Severidad))
            .ThenBy(a => a.Tipo)
            .ThenBy(a => a.Hotel)
            .ThenBy(a => a.Titulo)
            .Take(200)
            .ToList();

        return new AlertasResultadoDto(Resumen(ordenadas), ordenadas);
    }

    public async Task<AlertasResumenDto> ResumenAsync(CancellationToken ct = default) =>
        (await ListarAsync(ct)).Resumen;

    private async Task<List<AlertaDto>> AlertasStockAsync(
        List<HotelMini> hoteles, HashSet<int> hotelIds, CancellationToken ct)
    {
        var productos = await db.Productos
            .Include(p => p.UnidadBase)
            .Where(p => p.Activo)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Categoria,
                UnidadBase = p.UnidadBase.Nombre,
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var compras = await db.Detalles
            .Where(d => d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido
                        && hotelIds.Contains(d.DocumentoCompra.HotelId))
            .GroupBy(d => new { d.DocumentoCompra.HotelId, d.ProductoId })
            .Select(g => new { g.Key.HotelId, g.Key.ProductoId, Cantidad = g.Sum(d => d.Cantidad * d.FactorABase) })
            .ToListAsync(ct);

        var movimientos = await db.Movimientos
            .Where(m => hotelIds.Contains(m.HotelId))
            .GroupBy(m => new { m.HotelId, m.ProductoId, m.Tipo })
            .Select(g => new { g.Key.HotelId, g.Key.ProductoId, g.Key.Tipo, Cantidad = g.Sum(m => m.CantidadBase) })
            .ToListAsync(ct);

        var minimos = await db.StockMinimos
            .Where(s => hotelIds.Contains(s.HotelId))
            .Select(s => new { s.HotelId, s.ProductoId, s.CantidadMinimaBase })
            .ToListAsync(ct);

        var precios = await CargarUltimosPreciosAsync(ct);
        var hotelNombres = hoteles.ToDictionary(h => h.Id, h => h.Nombre);
        var existencia = new Dictionary<(int HotelId, int ProductoId), decimal>();

        foreach (var compra in compras)
            Sumar(existencia, (compra.HotelId, compra.ProductoId), compra.Cantidad);

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
            Sumar(existencia, (movimiento.HotelId, movimiento.ProductoId), efecto);
        }

        var minimoPorProducto = minimos.ToDictionary(m => (m.HotelId, m.ProductoId), m => m.CantidadMinimaBase);
        var llaves = minimoPorProducto.Keys
            .Concat(existencia.Where(e => e.Value < 0).Select(e => e.Key))
            .Distinct()
            .ToList();

        var alertas = new List<AlertaDto>();
        foreach (var llave in llaves)
        {
            if (!productos.TryGetValue(llave.ProductoId, out var producto)) continue;

            var stockMinimo = minimoPorProducto.GetValueOrDefault(llave);
            var actual = existencia.GetValueOrDefault(llave);
            if (actual >= stockMinimo && actual >= 0) continue;

            var faltante = stockMinimo > actual ? stockMinimo - actual : Math.Abs(actual);
            var valor = Math.Round(faltante * precios.GetValueOrDefault(llave.ProductoId), 2);
            var severidad = actual < 0
                ? "Critica"
                : stockMinimo > 0 && faltante >= stockMinimo * 0.5m
                    ? "Alta"
                    : "Media";

            alertas.Add(new AlertaDto(
                $"stock-{llave.HotelId}-{llave.ProductoId}",
                "StockCritico",
                severidad,
                $"{producto.Nombre} bajo minimo",
                $"Existencia {Math.Round(actual, 2)} {producto.UnidadBase}; minimo {Math.Round(stockMinimo, 2)}.",
                llave.HotelId,
                hotelNombres.GetValueOrDefault(llave.HotelId),
                "Producto",
                llave.ProductoId,
                valor,
                null,
                "Revisar sugerencia de compra o conteo fisico."));
        }

        return alertas;
    }

    private async Task<List<AlertaDto>> AlertasCuentasVencidasAsync(HashSet<int> hotelIds, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var documentos = await db.Documentos
            .Include(d => d.Hotel)
            .Include(d => d.Proveedor)
            .Include(d => d.Detalles)
            .Include(d => d.Pagos)
            .Where(d => d.Estado == EstadoDocumentoCompra.Recibido
                        && hotelIds.Contains(d.HotelId)
                        && (d.Observaciones ?? "") != DocumentoCompra.ObservacionImportadoExcel
                        && d.Proveedor.Nombre != Proveedor.NombreProveedorImportacionExcel
                        && d.Fecha <= hoy)
            .ToListAsync(ct);

        var alertas = new List<AlertaDto>();
        foreach (var doc in documentos)
        {
            var neto = Math.Max(0, doc.Total - doc.Retencion);
            var pagado = doc.Pagos.Sum(p => p.Monto);
            var saldo = Math.Max(0, neto - pagado);
            var vencimiento = doc.Fecha.AddDays(doc.Proveedor.DiasCredito);
            if (saldo <= 0 || vencimiento >= hoy) continue;

            var dias = hoy.DayNumber - vencimiento.DayNumber;
            var severidad = dias > 60 ? "Critica" : dias > 30 ? "Alta" : "Media";
            alertas.Add(new AlertaDto(
                $"cxp-{doc.Id}",
                "CuentaVencida",
                severidad,
                $"Factura {doc.NumeroDocumento} vencida",
                $"{doc.Proveedor.Nombre} tiene saldo pendiente de Q{saldo:N2} con {dias} dias vencidos.",
                doc.HotelId,
                doc.Hotel.Nombre,
                "DocumentoCompra",
                doc.Id,
                Math.Round(saldo, 2),
                vencimiento,
                "Registrar pago o coordinar liquidacion con proveedor."));
        }

        return alertas;
    }

    private async Task<List<AlertaDto>> AlertasConteosAsync(HashSet<int> hotelIds, CancellationToken ct)
    {
        var desde = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-60);
        var conteos = await db.ConteosInventario
            .Include(c => c.Hotel)
            .Include(c => c.Detalles)
            .Where(c => hotelIds.Contains(c.HotelId)
                        && c.Estado != EstadoConteoInventario.Anulado
                        && c.Fecha >= desde)
            .ToListAsync(ct);

        return conteos
            .Select(c => new
            {
                Conteo = c,
                Valor = c.Detalles.Sum(d => Math.Abs(d.ValorDiferenciaEstimado)),
                Diferencias = c.Detalles.Count(d => d.DiferenciaBase != 0),
            })
            .Where(c => c.Valor >= UmbralDiferenciaConteo)
            .Select(c => new AlertaDto(
                $"conteo-{c.Conteo.Id}",
                "ConteoDiferencia",
                c.Valor >= 1000 ? "Critica" : c.Valor >= 500 ? "Alta" : "Media",
                $"Conteo #{c.Conteo.Id} con diferencias fuertes",
                $"{c.Diferencias} productos con diferencias por Q{c.Valor:N2}.",
                c.Conteo.HotelId,
                c.Conteo.Hotel.Nombre,
                "ConteoInventario",
                c.Conteo.Id,
                Math.Round(c.Valor, 2),
                c.Conteo.Fecha,
                c.Conteo.Estado == EstadoConteoInventario.Registrado ? "Revisar y aplicar ajustes si procede." : "Revisar diferencias ajustadas."))
            .ToList();
    }

    private async Task<List<AlertaDto>> AlertasCierresPendientesAsync(List<HotelMini> hoteles, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodo = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        var hotelIds = hoteles.Select(h => h.Id).ToHashSet();
        var cerrados = await db.CierresMensuales
            .Where(c => hotelIds.Contains(c.HotelId)
                        && c.Anio == periodo.Year
                        && c.Mes == periodo.Month
                        && c.Estado == EstadoCierreMensual.Cerrado)
            .Select(c => c.HotelId)
            .ToListAsync(ct);
        var cerradosSet = cerrados.ToHashSet();

        return hoteles
            .Where(h => !cerradosSet.Contains(h.Id))
            .Select(h => new AlertaDto(
                $"cierre-{h.Id}-{periodo.Year}-{periodo.Month}",
                "CierrePendiente",
                hoy.Day >= 10 ? "Critica" : "Alta",
                $"Cierre pendiente de {periodo.Month}/{periodo.Year}",
                $"{h.Nombre} no tiene cierre mensual vigente para el periodo anterior.",
                h.Id,
                h.Nombre,
                "CierreMensual",
                null,
                null,
                periodo,
                "Generar cierre mensual o revisar si debe anularse/corregirse."))
            .ToList();
    }

    private async Task<Dictionary<int, decimal>> CargarUltimosPreciosAsync(CancellationToken ct)
    {
        var precios = await db.Detalles
            .Where(d => d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido && d.FactorABase > 0)
            .Select(d => new
            {
                d.Id,
                d.ProductoId,
                d.DocumentoCompra.Fecha,
                PrecioBase = d.PrecioUnitario / d.FactorABase,
            })
            .ToListAsync(ct);

        return precios
            .GroupBy(p => p.ProductoId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).First().PrecioBase);
    }

    private async Task<List<HotelMini>> CargarHotelesPermitidosAsync(CancellationToken ct)
    {
        var query = db.Hoteles.Where(h => h.Activo);
        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
        {
            var hoteles = currentUser.HotelesPermitidos;
            query = query.Where(h => hoteles.Contains(h.Id));
        }

        return await query
            .OrderBy(h => h.Nombre)
            .Select(h => new HotelMini(h.Id, h.Nombre))
            .ToListAsync(ct);
    }

    private static AlertasResumenDto Resumen(List<AlertaDto> alertas) => new(
        alertas.Count,
        alertas.Count(a => a.Severidad == "Critica"),
        alertas.Count(a => a.Severidad == "Alta"),
        alertas.Count(a => a.Severidad == "Media"),
        alertas.Count(a => a.Severidad == "Baja"));

    private static int Prioridad(string severidad) => severidad switch
    {
        "Critica" => 0,
        "Alta" => 1,
        "Media" => 2,
        _ => 3,
    };

    private static void Sumar<TKey>(Dictionary<TKey, decimal> diccionario, TKey llave, decimal valor)
        where TKey : notnull
    {
        diccionario[llave] = diccionario.GetValueOrDefault(llave) + valor;
    }

    private sealed record HotelMini(int Id, string Nombre);
}
