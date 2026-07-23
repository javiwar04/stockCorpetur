using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Catalogos;

public class ProductoService(IApplicationDbContext db) : IProductoService
{
    public async Task<List<ProductoDto>> ListarAsync(bool soloActivos, CancellationToken ct = default)
    {
        var query = db.Productos.Include(p => p.UnidadBase).AsQueryable();
        if (soloActivos) query = query.Where(p => p.Activo);

        return await query
            .OrderBy(p => p.Nombre)
            .Select(p => Mapear(p))
            .ToListAsync(ct);
    }

    public async Task<ProductoDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Productos.Include(x => x.UnidadBase).FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : Mapear(p);
    }

    public async Task<ProductoDto> CrearAsync(CrearProductoRequest req, CancellationToken ct = default)
    {
        var categoria = ParsearCategoria(req.Categoria);

        var producto = new Producto
        {
            Nombre = req.Nombre.Trim(),
            Categoria = categoria,
            UnidadBaseId = req.UnidadBaseId,
        };
        db.Productos.Add(producto);
        await db.SaveChangesAsync(ct);

        // La conversión a sí misma (factor 1) permite comprar en la unidad base sin configurar nada extra.
        db.Conversiones.Add(new ConversionProducto { ProductoId = producto.Id, UnidadId = req.UnidadBaseId, FactorABase = 1m });
        await db.SaveChangesAsync(ct);

        return (await ObtenerAsync(producto.Id, ct))!;
    }

    public async Task<ProductoDto?> ActualizarAsync(int id, ActualizarProductoRequest req, CancellationToken ct = default)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (producto is null) return null;

        var unidadBaseAnteriorId = producto.UnidadBaseId;

        producto.Nombre = req.Nombre.Trim();
        producto.Categoria = ParsearCategoria(req.Categoria);
        producto.UnidadBaseId = req.UnidadBaseId;
        producto.Activo = req.Activo;

        var conversionBase = await db.Conversiones
            .FirstOrDefaultAsync(c => c.ProductoId == id && c.UnidadId == req.UnidadBaseId, ct);

        if (unidadBaseAnteriorId != req.UnidadBaseId && conversionBase is { FactorABase: > 0 } conversionNuevaBase)
        {
            var factorNuevaBase = conversionNuevaBase.FactorABase;
            var conversiones = await db.Conversiones.Where(c => c.ProductoId == id).ToListAsync(ct);
            foreach (var conversion in conversiones)
                conversion.FactorABase = conversion.UnidadId == req.UnidadBaseId ? 1m : conversion.FactorABase / factorNuevaBase;
        }
        else if (conversionBase is null)
        {
            db.Conversiones.Add(new ConversionProducto
            {
                ProductoId = id,
                UnidadId = req.UnidadBaseId,
                FactorABase = 1m,
            });
        }
        else
        {
            conversionBase.FactorABase = 1m;
        }

        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<List<ConversionDto>> ListarConversionesAsync(int productoId, CancellationToken ct = default)
    {
        return await db.Conversiones
            .Include(c => c.Unidad)
            .Where(c => c.ProductoId == productoId)
            .Select(c => new ConversionDto(c.Id, c.UnidadId, c.Unidad.Nombre, c.FactorABase))
            .ToListAsync(ct);
    }

    public async Task<ConversionDto> AgregarConversionAsync(int productoId, CrearConversionRequest req, CancellationToken ct = default)
    {
        if (req.FactorABase <= 0)
            throw new InvalidOperationException("El factor de conversión debe ser mayor a cero.");

        DecimalPrecision.ValidarEscalaOperativa(req.FactorABase, "El factor de conversion");

        var existe = await db.Conversiones.AnyAsync(c => c.ProductoId == productoId && c.UnidadId == req.UnidadId, ct);
        if (existe)
            throw new InvalidOperationException("Ya existe una conversión para esa unidad en este producto.");

        var conversion = new ConversionProducto { ProductoId = productoId, UnidadId = req.UnidadId, FactorABase = req.FactorABase };
        db.Conversiones.Add(conversion);
        await db.SaveChangesAsync(ct);

        var unidad = await db.Unidades.FirstAsync(u => u.Id == req.UnidadId, ct);
        return new ConversionDto(conversion.Id, req.UnidadId, unidad.Nombre, req.FactorABase);
    }

    private static ProductoDto Mapear(Producto p) =>
        new(p.Id, p.Nombre, p.Categoria.ToString(), p.Activo, p.UnidadBaseId, p.UnidadBase.Nombre);

    private static CategoriaProducto ParsearCategoria(string valor) =>
        Enum.TryParse<CategoriaProducto>(valor, ignoreCase: true, out var cat)
            ? cat
            : throw new InvalidOperationException($"Categoría inválida: {valor}");
}
