using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Recetas;

public interface IRecetaService
{
    Task<List<PlatoDto>> ListarAsync(bool soloActivos, CancellationToken ct = default);
    Task<PlatoDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<PlatoDto> CrearAsync(CrearPlatoRequest req, CancellationToken ct = default);
    Task<PlatoDto?> ActualizarAsync(int id, ActualizarPlatoRequest req, CancellationToken ct = default);
    Task<PlatoDto?> UpsertIngredienteAsync(int platoId, UpsertIngredienteRequest req, CancellationToken ct = default);
    Task<PlatoDto?> EliminarIngredienteAsync(int platoId, int ingredienteId, CancellationToken ct = default);
    Task<List<ImpactoPlatoDto>> ImpactoProductoAsync(int productoId, CancellationToken ct = default);
}

/// <summary>
/// Costeo de menú. El costo de un plato se calcula con el precio ponderado de
/// los últimos 30 días de compras de cada ingrediente (todas las unidades
/// normalizadas a base); si no hubo compras recientes, cae al último precio
/// histórico conocido. Así el costo del plato "respira" con el mercado.
/// </summary>
public class RecetaService(IApplicationDbContext db) : IRecetaService
{
    public async Task<List<PlatoDto>> ListarAsync(bool soloActivos, CancellationToken ct = default)
    {
        var query = db.Platos.Include(p => p.Ingredientes).ThenInclude(i => i.Producto).ThenInclude(pr => pr.UnidadBase).AsQueryable();
        if (soloActivos) query = query.Where(p => p.Activo);

        var platos = await query.OrderBy(p => p.Nombre).ToListAsync(ct);
        var precios = await PreciosRecientesAsync(
            platos.SelectMany(p => p.Ingredientes.Select(i => i.ProductoId)).Distinct().ToList(), ct);

        return platos.Select(p => Mapear(p, precios)).ToList();
    }

    public async Task<PlatoDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var plato = await db.Platos
            .Include(p => p.Ingredientes).ThenInclude(i => i.Producto).ThenInclude(pr => pr.UnidadBase)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plato is null) return null;

        var precios = await PreciosRecientesAsync(plato.Ingredientes.Select(i => i.ProductoId).Distinct().ToList(), ct);
        return Mapear(plato, precios);
    }

    public async Task<PlatoDto> CrearAsync(CrearPlatoRequest req, CancellationToken ct = default)
    {
        ValidarPrecioVenta(req.PrecioVenta);
        var plato = new Plato { Nombre = req.Nombre.Trim(), PrecioVenta = req.PrecioVenta };
        db.Platos.Add(plato);
        await db.SaveChangesAsync(ct);
        return (await ObtenerAsync(plato.Id, ct))!;
    }

    public async Task<PlatoDto?> ActualizarAsync(int id, ActualizarPlatoRequest req, CancellationToken ct = default)
    {
        ValidarPrecioVenta(req.PrecioVenta);
        var plato = await db.Platos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plato is null) return null;

        plato.Nombre = req.Nombre.Trim();
        plato.PrecioVenta = req.PrecioVenta;
        plato.Activo = req.Activo;
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<PlatoDto?> UpsertIngredienteAsync(int platoId, UpsertIngredienteRequest req, CancellationToken ct = default)
    {
        if (req.CantidadPorPorcion <= 0)
            throw new InvalidOperationException("La cantidad por porción debe ser mayor a cero.");

        DecimalPrecision.ValidarEscalaOperativa(req.CantidadPorPorcion, "La cantidad por porcion");

        var plato = await db.Platos.FirstOrDefaultAsync(p => p.Id == platoId, ct);
        if (plato is null) return null;

        var existeProducto = await db.Productos.AnyAsync(p => p.Id == req.ProductoId, ct);
        if (!existeProducto)
            throw new InvalidOperationException("El producto no existe.");

        var ingrediente = await db.Recetas.FirstOrDefaultAsync(
            r => r.PlatoId == platoId && r.ProductoId == req.ProductoId, ct);

        if (ingrediente is null)
            db.Recetas.Add(new RecetaDetalle
            {
                PlatoId = platoId,
                ProductoId = req.ProductoId,
                CantidadPorPorcion = req.CantidadPorPorcion,
            });
        else
            ingrediente.CantidadPorPorcion = req.CantidadPorPorcion;

        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(platoId, ct);
    }

    public async Task<PlatoDto?> EliminarIngredienteAsync(int platoId, int ingredienteId, CancellationToken ct = default)
    {
        var ingrediente = await db.Recetas.FirstOrDefaultAsync(
            r => r.Id == ingredienteId && r.PlatoId == platoId, ct);
        if (ingrediente is null) return null;

        db.Recetas.Remove(ingrediente);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(platoId, ct);
    }

    public async Task<List<ImpactoPlatoDto>> ImpactoProductoAsync(int productoId, CancellationToken ct = default)
    {
        var platos = await db.Platos
            .Include(p => p.Ingredientes).ThenInclude(i => i.Producto).ThenInclude(pr => pr.UnidadBase)
            .Where(p => p.Activo && p.Ingredientes.Any(i => i.ProductoId == productoId))
            .ToListAsync(ct);
        if (platos.Count == 0) return [];

        var precios = await PreciosRecientesAsync(
            platos.SelectMany(p => p.Ingredientes.Select(i => i.ProductoId)).Distinct().ToList(), ct);

        return platos
            .Select(p =>
            {
                var dto = Mapear(p, precios);
                var linea = dto.Ingredientes.First(i => i.ProductoId == productoId);
                return new ImpactoPlatoDto(
                    p.Id, p.Nombre, linea.CantidadPorPorcion, linea.CostoLinea, dto.Costo,
                    dto.Costo == 0 ? 0 : Math.Round(linea.CostoLinea / dto.Costo * 100, 1));
            })
            .OrderByDescending(x => x.PorcentajeDelCosto)
            .ToList();
    }

    // --- Costeo ---

    /// <summary>
    /// Precio por unidad base de cada producto: ponderado de los últimos 30 días;
    /// si no hay compras recientes, el del documento más reciente que lo incluya.
    /// </summary>
    private async Task<Dictionary<int, decimal>> PreciosRecientesAsync(List<int> productoIds, CancellationToken ct)
    {
        if (productoIds.Count == 0) return [];

        var desde = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);

        var recientes = await db.Detalles
            .Where(d => productoIds.Contains(d.ProductoId)
                        && d.DocumentoCompra.Fecha >= desde
                        && d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido)
            .GroupBy(d => d.ProductoId)
            .Select(g => new
            {
                ProductoId = g.Key,
                Precio = g.Sum(d => d.Cantidad * d.PrecioUnitario - d.Descuento) / g.Sum(d => d.Cantidad * d.FactorABase),
            })
            .ToDictionaryAsync(x => x.ProductoId, x => x.Precio, ct);

        var faltantes = productoIds.Where(id => !recientes.ContainsKey(id)).ToList();
        if (faltantes.Count > 0)
        {
            // Último precio histórico conocido por producto.
            var ultimos = await db.Detalles
                .Where(d => faltantes.Contains(d.ProductoId)
                            && d.DocumentoCompra.Estado == EstadoDocumentoCompra.Recibido)
                .OrderByDescending(d => d.DocumentoCompra.Fecha).ThenByDescending(d => d.Id)
                .ToListAsync(ct);

            foreach (var grupo in ultimos.GroupBy(d => d.ProductoId))
            {
                var ultimo = grupo.First();
                recientes[grupo.Key] = ultimo.PrecioPorUnidadBase;
            }
        }

        return recientes;
    }

    private static PlatoDto Mapear(Plato plato, Dictionary<int, decimal> precios)
    {
        var ingredientes = plato.Ingredientes
            .OrderBy(i => i.Producto.Nombre)
            .Select(i =>
            {
                var tienePrecio = precios.TryGetValue(i.ProductoId, out var precio);
                return new IngredienteDto(
                    i.Id, i.ProductoId, i.Producto.Nombre, i.Producto.UnidadBase.Nombre,
                    i.CantidadPorPorcion,
                    Math.Round(precio, 4),
                    Math.Round(i.CantidadPorPorcion * precio, 4),
                    tienePrecio);
            })
            .ToList();

        var costo = Math.Round(ingredientes.Sum(i => i.CostoLinea), 4);
        var costoCompleto = ingredientes.All(i => i.TienePrecio);

        decimal? margen = plato.PrecioVenta is { } pv ? Math.Round(pv - costo, 4) : null;
        decimal? foodCost = plato.PrecioVenta is > 0 ? Math.Round(costo / plato.PrecioVenta.Value * 100, 1) : null;

        return new PlatoDto(
            plato.Id, plato.Nombre, plato.PrecioVenta, plato.Activo,
            costo, costoCompleto, margen, foodCost, ingredientes);
    }

    private static void ValidarPrecioVenta(decimal? precio)
    {
        if (precio is < 0)
            throw new InvalidOperationException("El precio de venta no puede ser negativo.");
    }
}
