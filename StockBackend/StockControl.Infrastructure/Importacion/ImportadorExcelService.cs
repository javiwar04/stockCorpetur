using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common;
using StockControl.Application.Common.Interfaces;
using StockControl.Application.Importacion;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Infrastructure.Importacion;

/// <summary>
/// Lee el libro mensual de los hoteles: una hoja por hotel; fila de fechas,
/// fila de números de documento y, desde la fila de productos, grupos de
/// 3 columnas (Cantidad/Precio/Total) por cada documento. La columna B marca
/// el inicio de cada categoría (VERDURA, FRUTAS, CONDIMENTOS, OTROS).
/// </summary>
public class ImportadorExcelService(IApplicationDbContext db) : IImportadorExcelService
{
    private const int ColumnaCategoria = 2;   // B
    private const int ColumnaProducto = 7;    // G
    private const int PrimeraColumnaDocs = 8; // H

    public async Task<ResultadoImportacion> ImportarAsync(Stream archivo, CancellationToken ct = default)
    {
        using var libro = new XLWorkbook(archivo);

        var hoteles = await db.Hoteles.ToListAsync(ct);
        var unidadBase = await db.Unidades.FirstOrDefaultAsync(u => u.Nombre == "Libra", ct)
                         ?? await db.Unidades.OrderBy(u => u.Id).FirstAsync(ct);
        var proveedor = await ObtenerOCrearProveedorAsync(ct);

        // Cache de productos por nombre normalizado (existentes + creados durante la importación).
        var productos = new Dictionary<string, Producto>();
        foreach (var p in await db.Productos.ToListAsync(ct))
            productos.TryAdd(Normalizar(p.Nombre), p);

        int hojas = 0, docsCreados = 0, docsOmitidos = 0, productosCreados = 0, lineas = 0;
        var hojasNoReconocidas = new List<string>();
        var advertencias = new List<string>();

        foreach (var hoja in libro.Worksheets)
        {
            var nombreHoja = hoja.Name.Trim();
            if (Normalizar(nombreHoja) == "consolidado") continue;

            var hotel = hoteles.FirstOrDefault(h => Normalizar(h.Nombre) == Normalizar(nombreHoja));
            if (hotel is null)
            {
                hojasNoReconocidas.Add(nombreHoja);
                continue;
            }

            // Localiza las filas clave en vez de asumir posiciones exactas.
            var filaNumeroDoc = BuscarFila(hoja, ColumnaProducto, "numero de documento");
            var filaProductos = BuscarFila(hoja, ColumnaProducto, "productos");
            if (filaNumeroDoc == 0 || filaProductos == 0)
            {
                advertencias.Add($"Hoja '{nombreHoja}': no se encontró el encabezado esperado, se omitió.");
                continue;
            }
            var filaFecha = filaNumeroDoc - 1;
            var primeraFilaDatos = filaProductos + 1;
            var ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? primeraFilaDatos;

            hojas++;

            var numerosExistentes = (await db.Documentos
                    .Where(d => d.HotelId == hotel.Id)
                    .Select(d => d.NumeroDocumento)
                    .ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var col = PrimeraColumnaDocs; ; col += 3)
            {
                var numeroDoc = hoja.Cell(filaNumeroDoc, col).GetString().Trim();
                if (string.IsNullOrEmpty(numeroDoc)) break;

                if (numerosExistentes.Contains(numeroDoc))
                {
                    docsOmitidos++;
                    continue;
                }

                var fecha = LeerFecha(hoja.Cell(filaFecha, col));
                if (fecha is null)
                {
                    docsOmitidos++;
                    advertencias.Add($"'{nombreHoja}' doc {numeroDoc}: sin fecha válida, se omitió.");
                    continue;
                }

                var documento = new DocumentoCompra
                {
                    Fecha = fecha.Value,
                    NumeroDocumento = numeroDoc,
                    NumeroPedido = numeroDoc,
                    HotelId = hotel.Id,
                    ProveedorId = proveedor.Id,
                    Observaciones = DocumentoCompra.ObservacionImportadoExcel,
                };

                var categoria = CategoriaProducto.Verdura;
                for (var fila = primeraFilaDatos; fila <= ultimaFila; fila++)
                {
                    var marcador = hoja.Cell(fila, ColumnaCategoria).GetString();
                    if (!string.IsNullOrWhiteSpace(marcador))
                        categoria = MapearCategoria(marcador) ?? categoria;

                    var nombreProducto = hoja.Cell(fila, ColumnaProducto).GetString().Trim();
                    if (string.IsNullOrEmpty(nombreProducto)) continue;

                    var cantidad = LeerDecimal(hoja.Cell(fila, col));
                    if (cantidad <= 0) continue;

                    var precio = LeerDecimal(hoja.Cell(fila, col + 1));
                    if (precio <= 0)
                    {
                        var total = LeerDecimal(hoja.Cell(fila, col + 2));
                        if (total > 0) precio = total / cantidad;
                        else continue; // sin precio ni total: línea inutilizable
                    }

                    var producto = ObtenerOCrearProducto(productos, nombreProducto, categoria, unidadBase.Id, ref productosCreados);

                    try
                    {
                        DecimalPrecision.ValidarEscalaOperativa(cantidad, "La cantidad");
                        DecimalPrecision.ValidarEscalaOperativa(precio, "El precio unitario");
                    }
                    catch (InvalidOperationException ex)
                    {
                        advertencias.Add($"'{nombreHoja}' doc {numeroDoc}, producto {nombreProducto}: {ex.Message}");
                        continue;
                    }

                    documento.Detalles.Add(new DetalleCompra
                    {
                        HotelId = hotel.Id,
                        Producto = producto,
                        UnidadId = unidadBase.Id,
                        Cantidad = cantidad,
                        PrecioUnitario = precio,
                        Descuento = 0m,
                        FactorABase = 1m,
                    });
                }

                if (documento.Detalles.Count == 0)
                {
                    docsOmitidos++;
                    continue;
                }

                db.Documentos.Add(documento);
                numerosExistentes.Add(numeroDoc);
                docsCreados++;
                lineas += documento.Detalles.Count;
            }
        }

        await db.SaveChangesAsync(ct);

        return new ResultadoImportacion(
            hojas, docsCreados, docsOmitidos, productosCreados, lineas, hojasNoReconocidas, advertencias);
    }

    // --- Auxiliares ---

    private async Task<Proveedor> ObtenerOCrearProveedorAsync(CancellationToken ct)
    {
        var proveedor = await db.Proveedores.FirstOrDefaultAsync(p => p.Nombre == Proveedor.NombreProveedorImportacionExcel, ct);
        if (proveedor is not null) return proveedor;

        proveedor = new Proveedor { Nombre = Proveedor.NombreProveedorImportacionExcel };
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync(ct);
        return proveedor;
    }

    private Producto ObtenerOCrearProducto(
        Dictionary<string, Producto> cache, string nombre, CategoriaProducto categoria, int unidadBaseId, ref int creados)
    {
        var clave = Normalizar(nombre);
        if (cache.TryGetValue(clave, out var existente)) return existente;

        var producto = new Producto
        {
            Nombre = nombre,
            Categoria = categoria,
            UnidadBaseId = unidadBaseId,
            Conversiones = { new ConversionProducto { UnidadId = unidadBaseId, FactorABase = 1m } },
        };
        db.Productos.Add(producto);
        cache[clave] = producto;
        creados++;
        return producto;
    }

    private static int BuscarFila(IXLWorksheet hoja, int columna, string texto)
    {
        for (var fila = 1; fila <= 30; fila++)
            if (Normalizar(hoja.Cell(fila, columna).GetString()).Contains(texto))
                return fila;
        return 0;
    }

    private static CategoriaProducto? MapearCategoria(string marcador)
    {
        var m = Normalizar(marcador);
        if (m.Contains("verdura")) return CategoriaProducto.Verdura;
        if (m.Contains("fruta")) return CategoriaProducto.Fruta;
        if (m.Contains("condimento") || m.Contains("especie")) return CategoriaProducto.Condimento;
        if (m.Contains("lacteo")) return CategoriaProducto.Lacteo;
        if (m.Contains("proteina") || m.Contains("carne")) return CategoriaProducto.Proteina;
        if (m.Contains("otro")) return CategoriaProducto.Otros;
        return null;
    }

    private static decimal LeerDecimal(IXLCell celda)
    {
        var valor = celda.HasFormula ? celda.CachedValue : celda.Value;
        return valor.IsNumber ? (decimal)valor.GetNumber() : 0m;
    }

    private static DateOnly? LeerFecha(IXLCell celda)
    {
        var valor = celda.HasFormula ? celda.CachedValue : celda.Value;
        if (valor.IsDateTime) return DateOnly.FromDateTime(valor.GetDateTime());
        if (valor.IsNumber) return DateOnly.FromDateTime(DateTime.FromOADate(valor.GetNumber()));
        if (valor.IsText && DateTime.TryParse(valor.GetText(), CultureInfo.GetCultureInfo("es-GT"), out var dt))
            return DateOnly.FromDateTime(dt);
        return null;
    }

    /// <summary>Minúsculas, sin tildes y sin espacios dobles, para comparar nombres tolerantemente.</summary>
    private static string Normalizar(string texto)
    {
        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
