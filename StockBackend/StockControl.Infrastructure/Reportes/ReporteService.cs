using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StockControl.Application.Common.Interfaces;
using StockControl.Application.Reportes;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Infrastructure.Reportes;

public class ReporteService(IApplicationDbContext db, ICurrentUser currentUser) : IReporteService
{
    public async Task<byte[]> GenerarExcelAsync(FiltroReporte filtro, CancellationToken ct = default)
    {
        var (documentos, titulo) = await CargarDatosAsync(filtro, ct);

        using var libro = new XLWorkbook();

        // --- Hoja 1: Documentos ---
        var hojaDocs = libro.Worksheets.Add("Documentos");
        hojaDocs.Cell(1, 1).Value = titulo;
        hojaDocs.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 14;
        hojaDocs.Range(1, 1, 1, 6).Merge();

        string[] encabezadosDoc = ["Fecha", "No. Documento", "Hotel", "Proveedor", "Tipo compra", "Total (Q)"];
        for (var i = 0; i < encabezadosDoc.Length; i++)
        {
            var celda = hojaDocs.Cell(3, i + 1);
            celda.Value = encabezadosDoc[i];
            celda.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#0f172a")).Font.SetFontColor(XLColor.White);
        }

        var fila = 4;
        foreach (var d in documentos)
        {
            hojaDocs.Cell(fila, 1).Value = d.Fecha.ToDateTime(TimeOnly.MinValue);
            hojaDocs.Cell(fila, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            hojaDocs.Cell(fila, 2).Value = d.NumeroDocumento;
            hojaDocs.Cell(fila, 3).Value = d.Hotel.Nombre;
            hojaDocs.Cell(fila, 4).Value = d.Proveedor.Nombre;
            hojaDocs.Cell(fila, 5).Value = d.TipoCompra.ToString();
            hojaDocs.Cell(fila, 6).FormulaA1 = $"=SUMIFS(Detalle!I:I,Detalle!B:B,B{fila},Detalle!C:C,C{fila})";
            hojaDocs.Cell(fila, 6).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }
        var filaTotal = fila + 1;
        hojaDocs.Cell(filaTotal, 5).Value = "GRAN TOTAL";
        hojaDocs.Cell(filaTotal, 5).Style.Font.SetBold();
        hojaDocs.Cell(filaTotal, 6).FormulaA1 = $"=SUM(F4:F{fila - 1})";
        hojaDocs.Cell(filaTotal, 6).Style.Font.SetBold().NumberFormat.Format = "#,##0.00";

        // --- Hoja 2: Detalle ---
        var hojaDet = libro.Worksheets.Add("Detalle");
        string[] encabezadosDet = ["Fecha", "No. Documento", "Hotel", "Tipo compra", "Producto", "Categoría", "Cantidad", "Precio unit. (Q)", "Total (Q)"];
        for (var i = 0; i < encabezadosDet.Length; i++)
        {
            var celda = hojaDet.Cell(1, i + 1);
            celda.Value = encabezadosDet[i];
            celda.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#0f172a")).Font.SetFontColor(XLColor.White);
        }

        fila = 2;
        foreach (var d in documentos)
        {
            foreach (var linea in d.Detalles)
            {
                hojaDet.Cell(fila, 1).Value = d.Fecha.ToDateTime(TimeOnly.MinValue);
                hojaDet.Cell(fila, 1).Style.DateFormat.Format = "dd/mm/yyyy";
                hojaDet.Cell(fila, 2).Value = d.NumeroDocumento;
                hojaDet.Cell(fila, 3).Value = d.Hotel.Nombre;
                hojaDet.Cell(fila, 4).Value = d.TipoCompra.ToString();
                hojaDet.Cell(fila, 5).Value = linea.Producto.Nombre;
                hojaDet.Cell(fila, 6).Value = linea.Producto.Categoria.ToString();
                hojaDet.Cell(fila, 7).Value = linea.Cantidad;
                hojaDet.Cell(fila, 8).Value = linea.PrecioUnitario;
                hojaDet.Cell(fila, 8).Style.NumberFormat.Format = "#,##0.00";
                hojaDet.Cell(fila, 9).FormulaA1 = $"=G{fila}*H{fila}";
                hojaDet.Cell(fila, 9).Style.NumberFormat.Format = "#,##0.00";
                fila++;
            }
        }

        // --- Hoja 3: Resumen por producto ---
        var resumen = documentos
            .SelectMany(d => d.Detalles)
            .GroupBy(l => new { l.Producto.Nombre, l.Producto.Categoria })
            .Select(g => new
            {
                g.Key.Nombre,
                Categoria = g.Key.Categoria.ToString(),
                Cantidad = g.Sum(l => l.Cantidad * l.FactorABase),
                Gasto = g.Sum(l => l.Cantidad * l.PrecioUnitario),
            })
            .OrderByDescending(x => x.Gasto)
            .ToList();

        var hojaRes = libro.Worksheets.Add("Resumen productos");
        string[] encabezadosRes = ["Producto", "Categoría", "Cantidad (unidad base)", "Gasto (Q)", "Precio prom. (Q)"];
        for (var i = 0; i < encabezadosRes.Length; i++)
        {
            var celda = hojaRes.Cell(1, i + 1);
            celda.Value = encabezadosRes[i];
            celda.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#0f172a")).Font.SetFontColor(XLColor.White);
        }
        fila = 2;
        foreach (var r in resumen)
        {
            hojaRes.Cell(fila, 1).Value = r.Nombre;
            hojaRes.Cell(fila, 2).Value = r.Categoria;
            hojaRes.Cell(fila, 3).Value = r.Cantidad;
            hojaRes.Cell(fila, 4).Value = r.Gasto;
            hojaRes.Cell(fila, 4).Style.NumberFormat.Format = "#,##0.00";
            hojaRes.Cell(fila, 5).FormulaA1 = $"=IF(C{fila}=0,0,D{fila}/C{fila})";
            hojaRes.Cell(fila, 5).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }

        // --- Hoja 4: Resumen por tipo de compra ---
        var resumenTipo = documentos
            .GroupBy(d => d.TipoCompra)
            .Select(g => new
            {
                Tipo = g.Key.ToString(),
                Documentos = g.Count(),
                Bruto = g.Sum(d => d.Detalles.Sum(l => l.Cantidad * l.PrecioUnitario)),
                Retencion = g.Sum(d => d.Retencion),
            })
            .Select(x => new
            {
                x.Tipo,
                x.Documentos,
                x.Bruto,
                x.Retencion,
                Neto = x.Bruto - x.Retencion,
                Promedio = x.Documentos == 0 ? 0 : x.Bruto / x.Documentos,
            })
            .OrderBy(x => x.Tipo)
            .ToList();

        var hojaTipo = libro.Worksheets.Add("Resumen tipo compra");
        string[] encabezadosTipo = ["Tipo compra", "Documentos", "Bruto (Q)", "Retencion (Q)", "Neto (Q)", "Promedio doc. (Q)"];
        for (var i = 0; i < encabezadosTipo.Length; i++)
        {
            var celda = hojaTipo.Cell(1, i + 1);
            celda.Value = encabezadosTipo[i];
            celda.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#0f172a")).Font.SetFontColor(XLColor.White);
        }
        fila = 2;
        foreach (var t in resumenTipo)
        {
            hojaTipo.Cell(fila, 1).Value = t.Tipo;
            hojaTipo.Cell(fila, 2).Value = t.Documentos;
            hojaTipo.Cell(fila, 3).Value = t.Bruto;
            hojaTipo.Cell(fila, 4).Value = t.Retencion;
            hojaTipo.Cell(fila, 5).Value = t.Neto;
            hojaTipo.Cell(fila, 6).Value = t.Promedio;
            hojaTipo.Range(fila, 3, fila, 6).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }

        // --- Hoja 5: Liquidacion por proveedor ---
        var liquidacion = documentos
            .GroupBy(d => new { d.ProveedorId, d.Proveedor.Nombre, d.Proveedor.Nit })
            .Select(g => new
            {
                Proveedor = g.Key.Nombre,
                Nit = g.Key.Nit,
                Documentos = g.Count(),
                Desde = g.Min(d => d.Fecha),
                Hasta = g.Max(d => d.Fecha),
                Bruto = g.Sum(d => d.Detalles.Sum(l => l.Cantidad * l.PrecioUnitario)),
                Retencion = g.Sum(d => d.Retencion),
            })
            .Select(x => new
            {
                x.Proveedor,
                x.Nit,
                x.Documentos,
                x.Desde,
                x.Hasta,
                x.Bruto,
                x.Retencion,
                Neto = x.Bruto - x.Retencion,
            })
            .OrderByDescending(x => x.Neto)
            .ToList();

        var hojaLiq = libro.Worksheets.Add("Liquidacion proveedores");
        hojaLiq.Cell(1, 1).Value = "Liquidacion de proveedores";
        hojaLiq.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 14;
        hojaLiq.Range(1, 1, 1, 8).Merge();
        string[] encabezadosLiq = ["Proveedor", "NIT", "Documentos", "Primera compra", "Ultima compra", "Bruto (Q)", "Retencion (Q)", "Neto a pagar (Q)"];
        for (var i = 0; i < encabezadosLiq.Length; i++)
        {
            var celda = hojaLiq.Cell(3, i + 1);
            celda.Value = encabezadosLiq[i];
            celda.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#0f172a")).Font.SetFontColor(XLColor.White);
        }

        fila = 4;
        foreach (var l in liquidacion)
        {
            hojaLiq.Cell(fila, 1).Value = l.Proveedor;
            hojaLiq.Cell(fila, 2).Value = l.Nit ?? "";
            hojaLiq.Cell(fila, 3).Value = l.Documentos;
            hojaLiq.Cell(fila, 4).Value = l.Desde.ToDateTime(TimeOnly.MinValue);
            hojaLiq.Cell(fila, 4).Style.DateFormat.Format = "dd/mm/yyyy";
            hojaLiq.Cell(fila, 5).Value = l.Hasta.ToDateTime(TimeOnly.MinValue);
            hojaLiq.Cell(fila, 5).Style.DateFormat.Format = "dd/mm/yyyy";
            hojaLiq.Cell(fila, 6).Value = l.Bruto;
            hojaLiq.Cell(fila, 7).Value = l.Retencion;
            hojaLiq.Cell(fila, 8).Value = l.Neto;
            hojaLiq.Range(fila, 6, fila, 8).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }
        hojaLiq.Cell(fila + 1, 5).Value = "TOTAL A PAGAR";
        hojaLiq.Cell(fila + 1, 5).Style.Font.SetBold();
        hojaLiq.Cell(fila + 1, 8).Value = liquidacion.Sum(l => l.Neto);
        hojaLiq.Cell(fila + 1, 8).Style.Font.SetBold().NumberFormat.Format = "#,##0.00";

        // --- Hoja 6: Facturas por proveedor ---
        var hojaProv = libro.Worksheets.Add("Facturas proveedor");
        string[] encabezadosProv = ["Fecha", "Proveedor", "NIT", "No. Documento", "Hotel", "Tipo compra", "Bruto (Q)", "Retencion (Q)", "Neto a pagar (Q)", "Observaciones"];
        for (var i = 0; i < encabezadosProv.Length; i++)
        {
            var celda = hojaProv.Cell(1, i + 1);
            celda.Value = encabezadosProv[i];
            celda.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#0f172a")).Font.SetFontColor(XLColor.White);
        }

        fila = 2;
        foreach (var d in documentos.OrderBy(d => d.Proveedor.Nombre).ThenBy(d => d.Fecha).ThenBy(d => d.NumeroDocumento))
        {
            var bruto = d.Detalles.Sum(l => l.Cantidad * l.PrecioUnitario);
            hojaProv.Cell(fila, 1).Value = d.Fecha.ToDateTime(TimeOnly.MinValue);
            hojaProv.Cell(fila, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            hojaProv.Cell(fila, 2).Value = d.Proveedor.Nombre;
            hojaProv.Cell(fila, 3).Value = d.Proveedor.Nit ?? "";
            hojaProv.Cell(fila, 4).Value = d.NumeroDocumento;
            hojaProv.Cell(fila, 5).Value = d.Hotel.Nombre;
            hojaProv.Cell(fila, 6).Value = d.TipoCompra.ToString();
            hojaProv.Cell(fila, 7).Value = bruto;
            hojaProv.Cell(fila, 8).Value = d.Retencion;
            hojaProv.Cell(fila, 9).Value = bruto - d.Retencion;
            hojaProv.Cell(fila, 10).Value = d.Observaciones ?? "";
            hojaProv.Range(fila, 7, fila, 9).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }

        foreach (var hoja in libro.Worksheets) hoja.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerarPdfAsync(FiltroReporte filtro, CancellationToken ct = default)
    {
        var (documentos, titulo) = await CargarDatosAsync(filtro, ct);

        var granTotal = documentos.Sum(d => d.Detalles.Sum(l => l.Cantidad * l.PrecioUnitario));
        var porCategoria = documentos
            .SelectMany(d => d.Detalles)
            .GroupBy(l => l.Producto.Categoria)
            .Select(g => new { Categoria = g.Key.ToString(), Gasto = g.Sum(l => l.Cantidad * l.PrecioUnitario) })
            .OrderByDescending(x => x.Gasto)
            .ToList();
        var porTipo = documentos
            .GroupBy(d => d.TipoCompra)
            .Select(g => new
            {
                Tipo = g.Key.ToString(),
                Documentos = g.Count(),
                Gasto = g.Sum(d => d.Detalles.Sum(l => l.Cantidad * l.PrecioUnitario)),
            })
            .OrderBy(x => x.Tipo)
            .ToList();
        var liquidacion = documentos
            .GroupBy(d => new { d.ProveedorId, d.Proveedor.Nombre, d.Proveedor.Nit })
            .Select(g => new
            {
                Proveedor = g.Key.Nombre,
                Nit = g.Key.Nit,
                Documentos = g.Count(),
                Bruto = g.Sum(d => d.Detalles.Sum(l => l.Cantidad * l.PrecioUnitario)),
                Retencion = g.Sum(d => d.Retencion),
            })
            .Select(x => new
            {
                x.Proveedor,
                x.Nit,
                x.Documentos,
                x.Bruto,
                x.Retencion,
                Neto = x.Bruto - x.Retencion,
            })
            .OrderByDescending(x => x.Neto)
            .ToList();
        var netoAPagar = liquidacion.Sum(l => l.Neto);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("StockControl — Reporte de compras").FontSize(16).Bold();
                    col.Item().Text(titulo).FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);

                    // KPIs
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Gasto total").FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Q{granTotal:N2}").FontSize(13).Bold();
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Neto a pagar").FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Q{netoAPagar:N2}").FontSize(13).Bold();
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Documentos").FontColor(Colors.Grey.Darken1);
                            c.Item().Text(documentos.Count.ToString()).FontSize(13).Bold();
                        });
                    });

                    // Liquidacion por proveedor
                    col.Item().Text("Liquidacion por proveedor").FontSize(11).Bold();
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.ConstantColumn(35);
                            c.ConstantColumn(65);
                            c.ConstantColumn(60);
                            c.ConstantColumn(70);
                        });

                        tabla.Header(h =>
                        {
                            foreach (var texto in new[] { "Proveedor", "NIT", "Docs", "Bruto", "Ret.", "Neto" })
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(texto).Bold();
                        });

                        foreach (var l in liquidacion)
                        {
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(l.Proveedor);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(l.Nit ?? "");
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(l.Documentos.ToString());
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"Q{l.Bruto:N2}");
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"Q{l.Retencion:N2}");
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"Q{l.Neto:N2}");
                        }
                    });

                    // Por categoría
                    col.Item().Text("Gasto por categoría").FontSize(11).Bold();
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });
                        foreach (var cat in porCategoria)
                        {
                            tabla.Cell().PaddingVertical(2).Text(cat.Categoria);
                            tabla.Cell().PaddingVertical(2).AlignRight().Text($"Q{cat.Gasto:N2}");
                        }
                    });

                    // Por tipo de compra
                    col.Item().Text("Gasto por tipo de compra").FontSize(11).Bold();
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.ConstantColumn(55);
                            c.ConstantColumn(80);
                        });
                        tabla.Header(h =>
                        {
                            foreach (var texto in new[] { "Tipo", "Docs", "Gasto" })
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(texto).Bold();
                        });
                        foreach (var tipo in porTipo)
                        {
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(tipo.Tipo);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(tipo.Documentos.ToString());
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"Q{tipo.Gasto:N2}");
                        }
                    });

                    // Documentos
                    col.Item().Text("Documentos").FontSize(11).Bold();
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(60);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.ConstantColumn(70);
                        });

                        tabla.Header(h =>
                        {
                            foreach (var texto in new[] { "Fecha", "No. Documento", "Hotel", "Proveedor", "Tipo", "Total" })
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(texto).Bold();
                        });

                        foreach (var d in documentos)
                        {
                            var total = d.Detalles.Sum(l => l.Cantidad * l.PrecioUnitario);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(d.Fecha.ToString("dd/MM/yyyy"));
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(d.NumeroDocumento);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(d.Hotel.Nombre);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(d.Proveedor.Nombre);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(d.TipoCompra.ToString());
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"Q{total:N2}");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm} — página ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(" de ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return pdf.GeneratePdf();
    }

    public async Task<byte[]> GenerarKardexExcelAsync(FiltroReporteKardex filtro, CancellationToken ct = default)
    {
        var kardex = await CargarKardexAsync(filtro, ct);

        using var libro = new XLWorkbook();

        var hojaResumen = libro.Worksheets.Add("Resumen");
        hojaResumen.Cell(1, 1).Value = "Kardex de producto";
        hojaResumen.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 14;
        hojaResumen.Range(1, 1, 1, 4).Merge();

        var resumen = new (string Etiqueta, string Valor)[]
        {
            ("Hotel", kardex.Hotel),
            ("Producto", kardex.Producto),
            ("Unidad base", kardex.UnidadBase),
            ("Desde", kardex.Desde?.ToString("dd/MM/yyyy") ?? "Inicio"),
            ("Hasta", kardex.Hasta?.ToString("dd/MM/yyyy") ?? "Actual"),
            ("Saldo inicial", kardex.SaldoInicial.ToString("N2")),
            ("Entradas", kardex.TotalEntradas.ToString("N2")),
            ("Salidas", kardex.TotalSalidas.ToString("N2")),
            ("Ajustes", kardex.TotalAjustes.ToString("N2")),
            ("Saldo final", kardex.SaldoFinal.ToString("N2")),
        };

        for (var i = 0; i < resumen.Length; i++)
        {
            hojaResumen.Cell(i + 3, 1).Value = resumen[i].Etiqueta;
            hojaResumen.Cell(i + 3, 1).Style.Font.SetBold();
            hojaResumen.Cell(i + 3, 2).Value = resumen[i].Valor;
        }

        var hoja = libro.Worksheets.Add("Kardex");
        string[] encabezados =
        [
            "Fecha", "Tipo", "Referencia", "Documento", "Proveedor", "Entrada",
            "Salida", "Ajuste", "Saldo", "Costo unit. (Q)", "Costo total (Q)", "Registro"
        ];
        AplicarEncabezados(hoja, encabezados, 1);

        var fila = 2;
        foreach (var m in kardex.Movimientos)
        {
            hoja.Cell(fila, 1).Value = m.Fecha.ToDateTime(TimeOnly.MinValue);
            hoja.Cell(fila, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            hoja.Cell(fila, 2).Value = m.Tipo;
            hoja.Cell(fila, 3).Value = m.Referencia;
            hoja.Cell(fila, 4).Value = m.Documento ?? "";
            hoja.Cell(fila, 5).Value = m.Proveedor ?? "";
            hoja.Cell(fila, 6).Value = m.Entrada;
            hoja.Cell(fila, 7).Value = m.Salida;
            hoja.Cell(fila, 8).Value = m.Ajuste;
            hoja.Cell(fila, 9).Value = m.Saldo;
            if (m.CostoUnitario is not null) hoja.Cell(fila, 10).Value = m.CostoUnitario.Value;
            if (m.CostoTotal is not null) hoja.Cell(fila, 11).Value = m.CostoTotal.Value;
            hoja.Cell(fila, 12).Value = m.CreadoPor ?? "";
            hoja.Range(fila, 6, fila, 11).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }

        hoja.Cell(fila + 1, 8).Value = "Saldo final";
        hoja.Cell(fila + 1, 8).Style.Font.SetBold();
        hoja.Cell(fila + 1, 9).Value = kardex.SaldoFinal;
        hoja.Cell(fila + 1, 9).Style.Font.SetBold().NumberFormat.Format = "#,##0.00";

        foreach (var ws in libro.Worksheets) ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerarCuentasPorPagarExcelAsync(FiltroReporteCuentasPorPagar filtro, CancellationToken ct = default)
    {
        var (cuentas, titulo, hoy) = await CargarCuentasPorPagarAsync(filtro, ct);
        var pendientes = cuentas.Where(c => c.Saldo > 0).ToList();

        using var libro = new XLWorkbook();

        var hojaResumen = libro.Worksheets.Add("Resumen");
        hojaResumen.Cell(1, 1).Value = "Cuentas por pagar";
        hojaResumen.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 14;
        hojaResumen.Range(1, 1, 1, 4).Merge();
        hojaResumen.Cell(2, 1).Value = titulo;
        hojaResumen.Range(2, 1, 2, 4).Merge();

        var resumen = new (string Etiqueta, decimal Valor)[]
        {
            ("Neto a pagar", cuentas.Sum(c => c.NetoAPagar)),
            ("Pagado", cuentas.Sum(c => c.Pagado)),
            ("Saldo pendiente", cuentas.Sum(c => c.Saldo)),
            ("Saldo vencido", cuentas.Where(c => c.Estado == "Vencido").Sum(c => c.Saldo)),
            ("Por vencer", pendientes.Where(c => c.FechaVencimiento >= hoy).Sum(c => c.Saldo)),
            ("Vencido 0-30", pendientes.Where(c => DiasVencido(c.FechaVencimiento, hoy) is >= 1 and <= 30).Sum(c => c.Saldo)),
            ("Vencido 31-60", pendientes.Where(c => DiasVencido(c.FechaVencimiento, hoy) is >= 31 and <= 60).Sum(c => c.Saldo)),
            ("Vencido 61+", pendientes.Where(c => DiasVencido(c.FechaVencimiento, hoy) >= 61).Sum(c => c.Saldo)),
        };

        for (var i = 0; i < resumen.Length; i++)
        {
            hojaResumen.Cell(i + 4, 1).Value = resumen[i].Etiqueta;
            hojaResumen.Cell(i + 4, 1).Style.Font.SetBold();
            hojaResumen.Cell(i + 4, 2).Value = resumen[i].Valor;
            hojaResumen.Cell(i + 4, 2).Style.NumberFormat.Format = "#,##0.00";
        }

        var hojaFacturas = libro.Worksheets.Add("Facturas");
        string[] encabezadosFacturas =
        [
            "Fecha", "Vence", "Dias credito", "Documento", "Hotel", "Proveedor",
            "Estado", "Bruto (Q)", "Retencion (Q)", "Neto (Q)", "Pagado (Q)", "Saldo (Q)"
        ];
        AplicarEncabezados(hojaFacturas, encabezadosFacturas, 1);

        var fila = 2;
        foreach (var c in cuentas)
        {
            hojaFacturas.Cell(fila, 1).Value = c.Fecha.ToDateTime(TimeOnly.MinValue);
            hojaFacturas.Cell(fila, 2).Value = c.FechaVencimiento.ToDateTime(TimeOnly.MinValue);
            hojaFacturas.Range(fila, 1, fila, 2).Style.DateFormat.Format = "dd/mm/yyyy";
            hojaFacturas.Cell(fila, 3).Value = c.DiasCredito;
            hojaFacturas.Cell(fila, 4).Value = c.NumeroDocumento;
            hojaFacturas.Cell(fila, 5).Value = c.Hotel;
            hojaFacturas.Cell(fila, 6).Value = c.Proveedor;
            hojaFacturas.Cell(fila, 7).Value = c.Estado;
            hojaFacturas.Cell(fila, 8).Value = c.Bruto;
            hojaFacturas.Cell(fila, 9).Value = c.Retencion;
            hojaFacturas.Cell(fila, 10).Value = c.NetoAPagar;
            hojaFacturas.Cell(fila, 11).Value = c.Pagado;
            hojaFacturas.Cell(fila, 12).Value = c.Saldo;
            hojaFacturas.Range(fila, 8, fila, 12).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }

        var hojaPagos = libro.Worksheets.Add("Pagos");
        string[] encabezadosPagos =
        [
            "Fecha pago", "Documento", "Hotel", "Proveedor", "Metodo", "Referencia",
            "Observaciones", "Monto (Q)", "Registrado por", "Registrado en"
        ];
        AplicarEncabezados(hojaPagos, encabezadosPagos, 1);

        fila = 2;
        foreach (var pago in cuentas.SelectMany(c => c.Pagos.Select(p => new { Cuenta = c, Pago = p }))
                     .OrderBy(x => x.Pago.Fecha)
                     .ThenBy(x => x.Cuenta.Proveedor)
                     .ThenBy(x => x.Cuenta.NumeroDocumento))
        {
            hojaPagos.Cell(fila, 1).Value = pago.Pago.Fecha.ToDateTime(TimeOnly.MinValue);
            hojaPagos.Cell(fila, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            hojaPagos.Cell(fila, 2).Value = pago.Cuenta.NumeroDocumento;
            hojaPagos.Cell(fila, 3).Value = pago.Cuenta.Hotel;
            hojaPagos.Cell(fila, 4).Value = pago.Cuenta.Proveedor;
            hojaPagos.Cell(fila, 5).Value = pago.Pago.MetodoPago;
            hojaPagos.Cell(fila, 6).Value = pago.Pago.Referencia ?? "";
            hojaPagos.Cell(fila, 7).Value = pago.Pago.Observaciones ?? "";
            hojaPagos.Cell(fila, 8).Value = pago.Pago.Monto;
            hojaPagos.Cell(fila, 8).Style.NumberFormat.Format = "#,##0.00";
            hojaPagos.Cell(fila, 9).Value = pago.Pago.CreadoPor ?? "";
            hojaPagos.Cell(fila, 10).Value = pago.Pago.CreadoEn;
            hojaPagos.Cell(fila, 10).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            fila++;
        }

        var hojaProveedores = libro.Worksheets.Add("Por proveedor");
        string[] encabezadosProveedores =
        [
            "Proveedor", "Documentos", "Neto (Q)", "Pagado (Q)", "Saldo (Q)", "Vencido (Q)"
        ];
        AplicarEncabezados(hojaProveedores, encabezadosProveedores, 1);

        fila = 2;
        foreach (var grupo in cuentas
                     .GroupBy(c => c.Proveedor)
                     .Select(g => new
                     {
                         Proveedor = g.Key,
                         Documentos = g.Count(),
                         Neto = g.Sum(c => c.NetoAPagar),
                         Pagado = g.Sum(c => c.Pagado),
                         Saldo = g.Sum(c => c.Saldo),
                         Vencido = g.Where(c => c.Estado == "Vencido").Sum(c => c.Saldo),
                     })
                     .OrderByDescending(g => g.Saldo))
        {
            hojaProveedores.Cell(fila, 1).Value = grupo.Proveedor;
            hojaProveedores.Cell(fila, 2).Value = grupo.Documentos;
            hojaProveedores.Cell(fila, 3).Value = grupo.Neto;
            hojaProveedores.Cell(fila, 4).Value = grupo.Pagado;
            hojaProveedores.Cell(fila, 5).Value = grupo.Saldo;
            hojaProveedores.Cell(fila, 6).Value = grupo.Vencido;
            hojaProveedores.Range(fila, 3, fila, 6).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }

        foreach (var ws in libro.Worksheets) ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerarConteosExcelAsync(FiltroReporteConteos filtro, CancellationToken ct = default)
    {
        var (conteos, titulo) = await CargarConteosAsync(filtro, ct);
        var detalles = conteos.SelectMany(c => c.Detalles).ToList();

        using var libro = new XLWorkbook();

        var hojaResumen = libro.Worksheets.Add("Resumen");
        hojaResumen.Cell(1, 1).Value = "Reporte de conteos fisicos";
        hojaResumen.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 14;
        hojaResumen.Range(1, 1, 1, 4).Merge();
        hojaResumen.Cell(2, 1).Value = titulo;
        hojaResumen.Range(2, 1, 2, 4).Merge();

        var resumen = new (string Etiqueta, decimal Valor)[]
        {
            ("Conteos", conteos.Count),
            ("Productos contados", detalles.Count),
            ("Productos con diferencia", detalles.Count(d => d.DiferenciaBase != 0)),
            ("Faltantes", detalles.Count(d => d.DiferenciaBase < 0)),
            ("Sobrantes", detalles.Count(d => d.DiferenciaBase > 0)),
            ("Valor faltantes (Q)", Math.Abs(detalles.Where(d => d.DiferenciaBase < 0).Sum(d => d.ValorDiferenciaEstimado))),
            ("Valor sobrantes (Q)", detalles.Where(d => d.DiferenciaBase > 0).Sum(d => d.ValorDiferenciaEstimado)),
            ("Valor total diferencias (Q)", detalles.Sum(d => Math.Abs(d.ValorDiferenciaEstimado))),
            ("Conteos ajustados", conteos.Count(c => c.Estado == "Ajustado")),
        };

        for (var i = 0; i < resumen.Length; i++)
        {
            hojaResumen.Cell(i + 4, 1).Value = resumen[i].Etiqueta;
            hojaResumen.Cell(i + 4, 1).Style.Font.SetBold();
            hojaResumen.Cell(i + 4, 2).Value = resumen[i].Valor;
            hojaResumen.Cell(i + 4, 2).Style.NumberFormat.Format = "#,##0.00";
        }

        var hojaConteos = libro.Worksheets.Add("Conteos");
        string[] encabezadosConteos =
        [
            "Id", "Fecha", "Hotel", "Estado", "Productos", "Con diferencia",
            "Valor diferencias (Q)", "Creado por", "Creado en", "Ajustado por", "Ajustado en", "Observaciones"
        ];
        AplicarEncabezados(hojaConteos, encabezadosConteos, 1);

        var fila = 2;
        foreach (var c in conteos)
        {
            hojaConteos.Cell(fila, 1).Value = c.Id;
            hojaConteos.Cell(fila, 2).Value = c.Fecha.ToDateTime(TimeOnly.MinValue);
            hojaConteos.Cell(fila, 2).Style.DateFormat.Format = "dd/mm/yyyy";
            hojaConteos.Cell(fila, 3).Value = c.Hotel;
            hojaConteos.Cell(fila, 4).Value = c.Estado;
            hojaConteos.Cell(fila, 5).Value = c.Detalles.Count;
            hojaConteos.Cell(fila, 6).Value = c.Detalles.Count(d => d.DiferenciaBase != 0);
            hojaConteos.Cell(fila, 7).Value = c.Detalles.Sum(d => Math.Abs(d.ValorDiferenciaEstimado));
            hojaConteos.Cell(fila, 7).Style.NumberFormat.Format = "#,##0.00";
            hojaConteos.Cell(fila, 8).Value = c.CreadoPor ?? "";
            hojaConteos.Cell(fila, 9).Value = c.CreadoEn;
            hojaConteos.Cell(fila, 9).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            hojaConteos.Cell(fila, 10).Value = c.AjustesAplicadosPor ?? "";
            if (c.AjustesAplicadosEn is not null)
            {
                hojaConteos.Cell(fila, 11).Value = c.AjustesAplicadosEn.Value;
                hojaConteos.Cell(fila, 11).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            }
            hojaConteos.Cell(fila, 12).Value = c.Observaciones ?? "";
            fila++;
        }

        var hojaDetalle = libro.Worksheets.Add("Detalle");
        string[] encabezadosDetalle =
        [
            "Conteo", "Fecha", "Hotel", "Estado", "Producto", "Categoria", "Sistema",
            "Fisico", "Diferencia", "Unidad base", "Valor diferencia (Q)", "Movimiento ajuste", "Observaciones"
        ];
        AplicarEncabezados(hojaDetalle, encabezadosDetalle, 1);

        fila = 2;
        foreach (var d in detalles.OrderByDescending(d => Math.Abs(d.ValorDiferenciaEstimado)))
        {
            hojaDetalle.Cell(fila, 1).Value = d.ConteoId;
            hojaDetalle.Cell(fila, 2).Value = d.Fecha.ToDateTime(TimeOnly.MinValue);
            hojaDetalle.Cell(fila, 2).Style.DateFormat.Format = "dd/mm/yyyy";
            hojaDetalle.Cell(fila, 3).Value = d.Hotel;
            hojaDetalle.Cell(fila, 4).Value = d.Estado;
            hojaDetalle.Cell(fila, 5).Value = d.Producto;
            hojaDetalle.Cell(fila, 6).Value = d.Categoria;
            hojaDetalle.Cell(fila, 7).Value = d.CantidadSistemaBase;
            hojaDetalle.Cell(fila, 8).Value = d.CantidadFisicaBase;
            hojaDetalle.Cell(fila, 9).Value = d.DiferenciaBase;
            hojaDetalle.Cell(fila, 10).Value = d.UnidadBase;
            hojaDetalle.Cell(fila, 11).Value = d.ValorDiferenciaEstimado;
            hojaDetalle.Range(fila, 7, fila, 11).Style.NumberFormat.Format = "#,##0.00";
            hojaDetalle.Cell(fila, 12).Value = d.MovimientoAjusteId?.ToString() ?? "";
            hojaDetalle.Cell(fila, 13).Value = d.Observaciones ?? "";
            fila++;
        }

        foreach (var ws in libro.Worksheets) ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerarConteosPdfAsync(FiltroReporteConteos filtro, CancellationToken ct = default)
    {
        var (conteos, titulo) = await CargarConteosAsync(filtro, ct);
        var detalles = conteos.SelectMany(c => c.Detalles).ToList();
        var valorTotal = detalles.Sum(d => Math.Abs(d.ValorDiferenciaEstimado));
        var valorFaltantes = Math.Abs(detalles.Where(d => d.DiferenciaBase < 0).Sum(d => d.ValorDiferenciaEstimado));
        var valorSobrantes = detalles.Where(d => d.DiferenciaBase > 0).Sum(d => d.ValorDiferenciaEstimado);
        var topDiferencias = detalles
            .Where(d => d.DiferenciaBase != 0)
            .OrderByDescending(d => Math.Abs(d.ValorDiferenciaEstimado))
            .Take(18)
            .ToList();

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("StockControl - Conteos fisicos").FontSize(16).Bold();
                    col.Item().Text(titulo).FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Valor diferencias").FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Q{valorTotal:N2}").FontSize(13).Bold();
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Faltantes").FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Q{valorFaltantes:N2}").FontSize(13).Bold();
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Sobrantes").FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Q{valorSobrantes:N2}").FontSize(13).Bold();
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Conteos").FontColor(Colors.Grey.Darken1);
                            c.Item().Text(conteos.Count.ToString()).FontSize(13).Bold();
                        });
                    });

                    col.Item().Text("Conteos").FontSize(11).Bold();
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(42);
                            c.ConstantColumn(62);
                            c.RelativeColumn(2);
                            c.ConstantColumn(58);
                            c.ConstantColumn(56);
                            c.ConstantColumn(76);
                        });

                        tabla.Header(h =>
                        {
                            foreach (var texto in new[] { "Id", "Fecha", "Hotel", "Estado", "Dif.", "Valor" })
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(texto).Bold();
                        });

                        foreach (var c in conteos.Take(20))
                        {
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(c.Id.ToString());
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(c.Fecha.ToString("dd/MM/yyyy"));
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(c.Hotel);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(c.Estado);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(c.Detalles.Count(d => d.DiferenciaBase != 0).ToString());
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"Q{c.Detalles.Sum(d => Math.Abs(d.ValorDiferenciaEstimado)):N2}");
                        }
                    });

                    col.Item().Text("Mayores diferencias").FontSize(11).Bold();
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.5f);
                            c.RelativeColumn(1.5f);
                            c.ConstantColumn(55);
                            c.ConstantColumn(55);
                            c.ConstantColumn(55);
                            c.ConstantColumn(70);
                        });

                        tabla.Header(h =>
                        {
                            foreach (var texto in new[] { "Producto", "Hotel", "Sistema", "Fisico", "Dif.", "Valor" })
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(texto).Bold();
                        });

                        foreach (var d in topDiferencias)
                        {
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(d.Producto);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(d.Hotel);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{d.CantidadSistemaBase:N2}");
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{d.CantidadFisicaBase:N2}");
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{d.DiferenciaBase:N2}");
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"Q{Math.Abs(d.ValorDiferenciaEstimado):N2}");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm} - pagina ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(" de ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return pdf.GeneratePdf();
    }

    public async Task<byte[]> GenerarCierresMensualesExcelAsync(FiltroReporteCierresMensuales filtro, CancellationToken ct = default)
    {
        var (cierres, titulo) = await CargarCierresMensualesAsync(filtro, ct);

        using var libro = new XLWorkbook();

        var hojaResumen = libro.Worksheets.Add("Resumen");
        hojaResumen.Cell(1, 1).Value = "Reporte de cierres mensuales";
        hojaResumen.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 14;
        hojaResumen.Range(1, 1, 1, 4).Merge();
        hojaResumen.Cell(2, 1).Value = titulo;
        hojaResumen.Range(2, 1, 2, 4).Merge();

        var resumen = new (string Etiqueta, decimal Valor)[]
        {
            ("Cierres", cierres.Count),
            ("Compras (Q)", cierres.Sum(c => c.ComprasTotal)),
            ("Inventario estimado (Q)", cierres.Sum(c => c.ValorInventarioEstimado)),
            ("Productos en riesgo", cierres.Sum(c => c.ProductosEnRiesgo)),
            ("Faltante estimado (Q)", cierres.Sum(c => c.ValorFaltanteEstimado)),
            ("Mermas (Q)", cierres.Sum(c => c.ValorMermasEstimado)),
            ("Ajustes (Q)", cierres.Sum(c => c.ValorAjustesEstimado)),
            ("Conteos fisicos", cierres.Sum(c => c.ConteosFisicos)),
            ("Diferencias conteo (Q)", cierres.Sum(c => c.ValorDiferenciasConteo)),
            ("Cuentas por pagar (Q)", cierres.Sum(c => c.SaldoCuentasPorPagar)),
            ("Cuentas vencidas (Q)", cierres.Sum(c => c.SaldoCuentasVencido)),
            ("Documentos vencidos", cierres.Sum(c => c.DocumentosVencidos)),
        };

        for (var i = 0; i < resumen.Length; i++)
        {
            hojaResumen.Cell(i + 4, 1).Value = resumen[i].Etiqueta;
            hojaResumen.Cell(i + 4, 1).Style.Font.SetBold();
            hojaResumen.Cell(i + 4, 2).Value = resumen[i].Valor;
            hojaResumen.Cell(i + 4, 2).Style.NumberFormat.Format = "#,##0.00";
        }

        var hojaCierres = libro.Worksheets.Add("Cierres");
        string[] encabezadosCierres =
        [
            "Id", "Hotel", "Anio", "Mes", "Estado", "Fecha cierre", "Compras (Q)", "Docs compra",
            "Inventario (Q)", "Productos riesgo", "Faltante (Q)", "Mermas (Q)", "Mov. merma",
            "Ajustes (Q)", "Mov. ajuste", "Conteos", "Dif. conteos (Q)", "CXP saldo (Q)",
            "CXP vencido (Q)", "Docs vencidos", "Creado por", "Observaciones"
        ];
        AplicarEncabezados(hojaCierres, encabezadosCierres, 1);

        var fila = 2;
        foreach (var c in cierres)
        {
            hojaCierres.Cell(fila, 1).Value = c.Id;
            hojaCierres.Cell(fila, 2).Value = c.Hotel;
            hojaCierres.Cell(fila, 3).Value = c.Anio;
            hojaCierres.Cell(fila, 4).Value = c.Mes;
            hojaCierres.Cell(fila, 5).Value = c.Estado;
            hojaCierres.Cell(fila, 6).Value = c.FechaCierre;
            hojaCierres.Cell(fila, 6).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            hojaCierres.Cell(fila, 7).Value = c.ComprasTotal;
            hojaCierres.Cell(fila, 8).Value = c.DocumentosCompra;
            hojaCierres.Cell(fila, 9).Value = c.ValorInventarioEstimado;
            hojaCierres.Cell(fila, 10).Value = c.ProductosEnRiesgo;
            hojaCierres.Cell(fila, 11).Value = c.ValorFaltanteEstimado;
            hojaCierres.Cell(fila, 12).Value = c.ValorMermasEstimado;
            hojaCierres.Cell(fila, 13).Value = c.MovimientosMerma;
            hojaCierres.Cell(fila, 14).Value = c.ValorAjustesEstimado;
            hojaCierres.Cell(fila, 15).Value = c.MovimientosAjuste;
            hojaCierres.Cell(fila, 16).Value = c.ConteosFisicos;
            hojaCierres.Cell(fila, 17).Value = c.ValorDiferenciasConteo;
            hojaCierres.Cell(fila, 18).Value = c.SaldoCuentasPorPagar;
            hojaCierres.Cell(fila, 19).Value = c.SaldoCuentasVencido;
            hojaCierres.Cell(fila, 20).Value = c.DocumentosVencidos;
            hojaCierres.Cell(fila, 21).Value = c.CreadoPor ?? "";
            hojaCierres.Cell(fila, 22).Value = c.Observaciones ?? "";
            hojaCierres.Range(fila, 7, fila, 19).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }

        var hojaHoteles = libro.Worksheets.Add("Por hotel");
        string[] encabezadosHoteles =
        [
            "Hotel", "Cierres", "Compras (Q)", "Inventario (Q)", "Productos riesgo",
            "Faltante (Q)", "Mermas (Q)", "Ajustes (Q)", "Conteos", "Dif. conteos (Q)",
            "CXP saldo (Q)", "CXP vencido (Q)", "Docs vencidos"
        ];
        AplicarEncabezados(hojaHoteles, encabezadosHoteles, 1);

        fila = 2;
        foreach (var grupo in cierres
                     .GroupBy(c => new { c.HotelId, c.Hotel })
                     .OrderBy(g => g.Key.Hotel))
        {
            hojaHoteles.Cell(fila, 1).Value = grupo.Key.Hotel;
            hojaHoteles.Cell(fila, 2).Value = grupo.Count();
            hojaHoteles.Cell(fila, 3).Value = grupo.Sum(c => c.ComprasTotal);
            hojaHoteles.Cell(fila, 4).Value = grupo.Sum(c => c.ValorInventarioEstimado);
            hojaHoteles.Cell(fila, 5).Value = grupo.Sum(c => c.ProductosEnRiesgo);
            hojaHoteles.Cell(fila, 6).Value = grupo.Sum(c => c.ValorFaltanteEstimado);
            hojaHoteles.Cell(fila, 7).Value = grupo.Sum(c => c.ValorMermasEstimado);
            hojaHoteles.Cell(fila, 8).Value = grupo.Sum(c => c.ValorAjustesEstimado);
            hojaHoteles.Cell(fila, 9).Value = grupo.Sum(c => c.ConteosFisicos);
            hojaHoteles.Cell(fila, 10).Value = grupo.Sum(c => c.ValorDiferenciasConteo);
            hojaHoteles.Cell(fila, 11).Value = grupo.Sum(c => c.SaldoCuentasPorPagar);
            hojaHoteles.Cell(fila, 12).Value = grupo.Sum(c => c.SaldoCuentasVencido);
            hojaHoteles.Cell(fila, 13).Value = grupo.Sum(c => c.DocumentosVencidos);
            hojaHoteles.Range(fila, 3, fila, 12).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }

        foreach (var ws in libro.Worksheets) ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerarCierresMensualesPdfAsync(FiltroReporteCierresMensuales filtro, CancellationToken ct = default)
    {
        var (cierres, titulo) = await CargarCierresMensualesAsync(filtro, ct);
        var compras = cierres.Sum(c => c.ComprasTotal);
        var inventario = cierres.Sum(c => c.ValorInventarioEstimado);
        var cxpVencido = cierres.Sum(c => c.SaldoCuentasVencido);
        var mermasAjustes = cierres.Sum(c => c.ValorMermasEstimado + c.ValorAjustesEstimado);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("StockControl - Cierres mensuales").FontSize(16).Bold();
                    col.Item().Text(titulo).FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Compras").FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Q{compras:N2}").FontSize(13).Bold();
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Inventario").FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Q{inventario:N2}").FontSize(13).Bold();
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("CXP vencido").FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Q{cxpVencido:N2}").FontSize(13).Bold();
                        });
                        row.ConstantItem(8);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Mermas + ajustes").FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Q{mermasAjustes:N2}").FontSize(13).Bold();
                        });
                    });

                    col.Item().Text("Cierres").FontSize(11).Bold();
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.ConstantColumn(44);
                            c.ConstantColumn(44);
                            c.ConstantColumn(72);
                            c.ConstantColumn(76);
                            c.ConstantColumn(72);
                            c.ConstantColumn(72);
                        });

                        tabla.Header(h =>
                        {
                            foreach (var texto in new[] { "Hotel", "Anio", "Mes", "Compras", "Inventario", "CXP vencido", "Riesgo" })
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(texto).Bold();
                        });

                        foreach (var c in cierres.Take(24))
                        {
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(c.Hotel);
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(c.Anio.ToString());
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(c.Mes.ToString());
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"Q{c.ComprasTotal:N2}");
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"Q{c.ValorInventarioEstimado:N2}");
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"Q{c.SaldoCuentasVencido:N2}");
                            tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(c.ProductosEnRiesgo.ToString());
                        }
                    });

                    if (cierres.Count == 0)
                        col.Item().Text("No hay cierres mensuales para los filtros seleccionados.").FontColor(Colors.Grey.Darken1);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm} - pagina ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(" de ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return pdf.GeneratePdf();
    }

    // --- Datos compartidos ---

    private async Task<(List<DocumentoCompra> Documentos, string Titulo)> CargarDatosAsync(
        FiltroReporte filtro, CancellationToken ct)
    {
        if (filtro.HotelId is { } hotelId && !currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var query = db.Documentos
            .Include(d => d.Hotel)
            .Include(d => d.Proveedor)
            .Include(d => d.Detalles).ThenInclude(l => l.Producto)
            .Where(d => d.Estado == EstadoDocumentoCompra.Recibido)
            .AsQueryable();

        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
        {
            var hoteles = currentUser.HotelesPermitidos;
            query = query.Where(d => hoteles.Contains(d.HotelId));
        }

        if (filtro.HotelId is not null) query = query.Where(d => d.HotelId == filtro.HotelId);
        if (filtro.ProveedorId is not null) query = query.Where(d => d.ProveedorId == filtro.ProveedorId);
        if (!string.IsNullOrWhiteSpace(filtro.TipoCompra))
        {
            var tipoCompra = ParsearTipoCompra(filtro.TipoCompra);
            query = query.Where(d => d.TipoCompra == tipoCompra);
        }
        if (filtro.Desde is not null) query = query.Where(d => d.Fecha >= filtro.Desde);
        if (filtro.Hasta is not null) query = query.Where(d => d.Fecha <= filtro.Hasta);

        var documentos = await query
            .OrderBy(d => d.Fecha)
            .ThenBy(d => d.Proveedor.Nombre)
            .ThenBy(d => d.Hotel.Nombre)
            .ToListAsync(ct);

        var partes = new List<string>();
        if (filtro.HotelId is not null && documentos.Count > 0)
            partes.Add($"Hotel: {documentos[0].Hotel.Nombre}");
        else if (filtro.HotelId is null)
            partes.Add("Todos los hoteles");
        if (filtro.ProveedorId is not null && documentos.Count > 0)
            partes.Add($"Proveedor: {documentos[0].Proveedor.Nombre}");
        else if (filtro.ProveedorId is null)
            partes.Add("Todos los proveedores");
        if (!string.IsNullOrWhiteSpace(filtro.TipoCompra))
            partes.Add($"Tipo: {ParsearTipoCompra(filtro.TipoCompra)}");
        if (filtro.Desde is not null) partes.Add($"desde {filtro.Desde:dd/MM/yyyy}");
        if (filtro.Hasta is not null) partes.Add($"hasta {filtro.Hasta:dd/MM/yyyy}");

        return (documentos, string.Join(" · ", partes));
    }

    private static TipoCompra ParsearTipoCompra(string valor) =>
        Enum.TryParse<TipoCompra>(valor, ignoreCase: true, out var tipo)
            ? tipo
            : throw new InvalidOperationException($"Tipo de compra invalido: {valor}");

    private async Task<KardexReporte> CargarKardexAsync(FiltroReporteKardex filtro, CancellationToken ct)
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
            .Select(d => new KardexLineaReporte(
                $"C-{d.Id}",
                d.DocumentoCompra.Fecha,
                0,
                "Compra",
                $"Compra {d.DocumentoCompra.NumeroDocumento}",
                d.CantidadBase,
                0m,
                0m,
                d.FactorABase == 0 ? null : Math.Round(d.PrecioPorUnidadBase, 2),
                Math.Round(d.Total, 2),
                d.DocumentoCompra.NumeroDocumento,
                d.DocumentoCompra.Proveedor.Nombre,
                d.DocumentoCompra.CreadoPor))
            .Concat(movimientos.Select(m =>
            {
                var entrada = m.Tipo == TipoMovimiento.Entrada ? m.CantidadBase : 0m;
                var salida = m.Tipo is TipoMovimiento.Salida or TipoMovimiento.Merma ? m.CantidadBase : 0m;
                var ajuste = m.Tipo == TipoMovimiento.Ajuste ? m.CantidadBase : 0m;

                return new KardexLineaReporte(
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

        var saldo = saldoInicial;
        var movimientosPeriodo = new List<KardexMovimientoReporte>();
        foreach (var linea in lineas.Where(l => filtro.Desde is null || l.Fecha >= filtro.Desde))
        {
            saldo += linea.Efecto;
            movimientosPeriodo.Add(new KardexMovimientoReporte(
                linea.Id,
                linea.Fecha,
                linea.Tipo,
                linea.Referencia,
                Math.Round(linea.Entrada, 2),
                Math.Round(linea.Salida, 2),
                Math.Round(linea.Ajuste, 2),
                Math.Round(saldo, 2),
                linea.CostoUnitario,
                linea.CostoTotal,
                linea.Documento,
                linea.Proveedor,
                linea.CreadoPor));
        }

        return new KardexReporte(
            hotel.Nombre,
            producto.Nombre,
            producto.UnidadBase.Nombre,
            filtro.Desde,
            filtro.Hasta,
            Math.Round(saldoInicial, 2),
            Math.Round(movimientosPeriodo.Sum(m => m.Entrada), 2),
            Math.Round(movimientosPeriodo.Sum(m => m.Salida), 2),
            Math.Round(movimientosPeriodo.Sum(m => m.Ajuste), 2),
            Math.Round(saldo, 2),
            movimientosPeriodo);
    }

    private async Task<(List<CuentaPorPagarReporte> Cuentas, string Titulo, DateOnly Hoy)> CargarCuentasPorPagarAsync(
        FiltroReporteCuentasPorPagar filtro, CancellationToken ct)
    {
        if (filtro.HotelId is { } hotelId && !currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var query = db.Documentos
            .Include(d => d.Hotel)
            .Include(d => d.Proveedor)
            .Include(d => d.Detalles)
            .Include(d => d.Pagos).ThenInclude(p => p.Proveedor)
            .Where(d => d.Estado == EstadoDocumentoCompra.Recibido)
            .Where(d => (d.Observaciones ?? "") != DocumentoCompra.ObservacionImportadoExcel)
            .Where(d => d.Proveedor.Nombre != Proveedor.NombreProveedorImportacionExcel)
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
            .Select(d => MapearCuentaReporte(d, hoy))
            .Where(c => !filtro.SoloPendientes || c.Saldo > 0)
            .OrderBy(c => c.Estado == "Vencido" ? 0 : c.Estado == "Parcial" ? 1 : c.Estado == "Pendiente" ? 2 : 3)
            .ThenBy(c => c.FechaVencimiento)
            .ThenBy(c => c.Proveedor)
            .ToList();

        var partes = new List<string>();
        if (filtro.HotelId is not null && cuentas.Count > 0)
            partes.Add($"Hotel: {cuentas[0].Hotel}");
        else if (filtro.HotelId is null)
            partes.Add("Todos los hoteles");
        if (filtro.ProveedorId is not null && cuentas.Count > 0)
            partes.Add($"Proveedor: {cuentas[0].Proveedor}");
        else if (filtro.ProveedorId is null)
            partes.Add("Todos los proveedores");
        if (filtro.Desde is not null) partes.Add($"desde {filtro.Desde:dd/MM/yyyy}");
        if (filtro.Hasta is not null) partes.Add($"hasta {filtro.Hasta:dd/MM/yyyy}");
        if (filtro.SoloPendientes) partes.Add("solo pendientes");

        return (cuentas, string.Join(" - ", partes), hoy);
    }

    private async Task<(List<ConteoReporte> Conteos, string Titulo)> CargarConteosAsync(
        FiltroReporteConteos filtro, CancellationToken ct)
    {
        if (filtro.HotelId is { } hotelId && !currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var query = db.ConteosInventario
            .Include(c => c.Hotel)
            .Include(c => c.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.UnidadBase)
            .AsQueryable();

        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
        {
            var hoteles = currentUser.HotelesPermitidos;
            query = query.Where(c => hoteles.Contains(c.HotelId));
        }

        if (filtro.HotelId is not null) query = query.Where(c => c.HotelId == filtro.HotelId);
        if (filtro.Desde is not null) query = query.Where(c => c.Fecha >= filtro.Desde);
        if (filtro.Hasta is not null) query = query.Where(c => c.Fecha <= filtro.Hasta);

        var entidades = await query
            .OrderByDescending(c => c.Fecha)
            .ThenByDescending(c => c.Id)
            .ToListAsync(ct);

        var conteos = entidades.Select(c => new ConteoReporte(
            c.Id,
            c.Fecha,
            c.Hotel.Nombre,
            c.Estado.ToString(),
            c.Observaciones,
            c.CreadoEn,
            c.CreadoPor,
            c.AjustesAplicadosEn,
            c.AjustesAplicadosPor,
            c.Detalles
                .OrderByDescending(d => Math.Abs(d.ValorDiferenciaEstimado))
                .ThenBy(d => d.Producto.Nombre)
                .Select(d => new ConteoDetalleReporte(
                    c.Id,
                    c.Fecha,
                    c.Hotel.Nombre,
                    c.Estado.ToString(),
                    c.Observaciones,
                    d.Producto.Nombre,
                    d.Producto.Categoria.ToString(),
                    d.Producto.UnidadBase.Nombre,
                    Math.Round(d.CantidadSistemaBase, 2),
                    Math.Round(d.CantidadFisicaBase, 2),
                    Math.Round(d.DiferenciaBase, 2),
                    Math.Round(d.ValorDiferenciaEstimado, 2),
                    d.MovimientoAjusteId))
                .ToList()))
            .ToList();

        var partes = new List<string>();
        if (filtro.HotelId is not null && conteos.Count > 0)
            partes.Add($"Hotel: {conteos[0].Hotel}");
        else if (filtro.HotelId is not null)
            partes.Add($"Hotel ID: {filtro.HotelId}");
        else
            partes.Add("Todos los hoteles");
        if (filtro.Desde is not null) partes.Add($"desde {filtro.Desde:dd/MM/yyyy}");
        if (filtro.Hasta is not null) partes.Add($"hasta {filtro.Hasta:dd/MM/yyyy}");

        return (conteos, string.Join(" - ", partes));
    }

    private async Task<(List<CierreMensualReporte> Cierres, string Titulo)> CargarCierresMensualesAsync(
        FiltroReporteCierresMensuales filtro, CancellationToken ct)
    {
        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
            throw new UnauthorizedAccessException("Solo Admin o Gerencia pueden exportar cierres mensuales.");

        if (filtro.HotelId is { } hotelId && !currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        if (filtro.Mes is < 1 or > 12)
            throw new InvalidOperationException("Mes invalido para reporte de cierres.");

        if (filtro.Anio is < 2020 or > 2100)
            throw new InvalidOperationException("Anio invalido para reporte de cierres.");

        var query = db.CierresMensuales
            .Include(c => c.Hotel)
            .AsQueryable();

        if (filtro.HotelId is not null) query = query.Where(c => c.HotelId == filtro.HotelId);
        if (filtro.Anio is not null) query = query.Where(c => c.Anio == filtro.Anio);
        if (filtro.Mes is not null) query = query.Where(c => c.Mes == filtro.Mes);

        var entidades = await query
            .OrderByDescending(c => c.Anio)
            .ThenByDescending(c => c.Mes)
            .ThenBy(c => c.Hotel.Nombre)
            .ToListAsync(ct);

        var cierres = entidades.Select(c => new CierreMensualReporte(
            c.Id,
            c.HotelId,
            c.Hotel.Nombre,
            c.Anio,
            c.Mes,
            c.Estado.ToString(),
            Math.Round(c.ComprasTotal, 2),
            c.DocumentosCompra,
            Math.Round(c.ValorInventarioEstimado, 2),
            c.ProductosEnRiesgo,
            Math.Round(c.ValorFaltanteEstimado, 2),
            Math.Round(c.ValorMermasEstimado, 2),
            c.MovimientosMerma,
            Math.Round(c.ValorAjustesEstimado, 2),
            c.MovimientosAjuste,
            c.ConteosFisicos,
            Math.Round(c.ValorDiferenciasConteo, 2),
            Math.Round(c.SaldoCuentasPorPagar, 2),
            Math.Round(c.SaldoCuentasVencido, 2),
            c.DocumentosVencidos,
            c.FechaCierre,
            c.Observaciones,
            c.CreadoPor))
            .ToList();

        var partes = new List<string>();
        if (filtro.HotelId is not null)
        {
            var hotel = cierres.FirstOrDefault()?.Hotel
                ?? await db.Hoteles.Where(h => h.Id == filtro.HotelId).Select(h => h.Nombre).FirstOrDefaultAsync(ct)
                ?? $"Hotel ID: {filtro.HotelId}";
            partes.Add($"Hotel: {hotel}");
        }
        else
        {
            partes.Add("Todos los hoteles");
        }

        if (filtro.Anio is not null) partes.Add($"anio {filtro.Anio}");
        if (filtro.Mes is not null) partes.Add($"mes {filtro.Mes}");

        return (cierres, string.Join(" - ", partes));
    }

    private static CuentaPorPagarReporte MapearCuentaReporte(DocumentoCompra d, DateOnly hoy)
    {
        var bruto = d.Total;
        var neto = Math.Max(0, bruto - d.Retencion);
        var pagado = d.Pagos.Sum(p => p.Monto);
        var saldo = Math.Max(0, neto - pagado);
        var fechaVencimiento = d.Fecha.AddDays(d.Proveedor.DiasCredito);

        return new CuentaPorPagarReporte(
            d.Fecha,
            fechaVencimiento,
            d.Proveedor.DiasCredito,
            d.NumeroDocumento,
            d.Hotel.Nombre,
            d.Proveedor.Nombre,
            EstadoCuenta(saldo, pagado, fechaVencimiento, hoy),
            Math.Round(bruto, 2),
            Math.Round(d.Retencion, 2),
            Math.Round(neto, 2),
            Math.Round(pagado, 2),
            Math.Round(saldo, 2),
            d.Pagos.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).Select(MapearPagoReporte).ToList());
    }

    private static PagoProveedorReporte MapearPagoReporte(PagoProveedor p) => new(
        p.Fecha,
        Math.Round(p.Monto, 2),
        p.MetodoPago,
        p.Referencia,
        p.Observaciones,
        p.CreadoPor,
        p.CreadoEn);

    private static string EstadoCuenta(decimal saldo, decimal pagado, DateOnly fechaVencimiento, DateOnly hoy)
    {
        if (saldo <= 0) return "Pagado";
        if (fechaVencimiento < hoy) return "Vencido";
        if (pagado > 0) return "Parcial";
        return "Pendiente";
    }

    private static int DiasVencido(DateOnly fechaVencimiento, DateOnly hoy) => hoy.DayNumber - fechaVencimiento.DayNumber;

    private static void AplicarEncabezados(IXLWorksheet hoja, IReadOnlyList<string> encabezados, int fila)
    {
        for (var i = 0; i < encabezados.Count; i++)
        {
            var celda = hoja.Cell(fila, i + 1);
            celda.Value = encabezados[i];
            celda.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#0f172a")).Font.SetFontColor(XLColor.White);
        }
    }

    private sealed record KardexReporte(
        string Hotel,
        string Producto,
        string UnidadBase,
        DateOnly? Desde,
        DateOnly? Hasta,
        decimal SaldoInicial,
        decimal TotalEntradas,
        decimal TotalSalidas,
        decimal TotalAjustes,
        decimal SaldoFinal,
        List<KardexMovimientoReporte> Movimientos);

    private sealed record KardexMovimientoReporte(
        string Id,
        DateOnly Fecha,
        string Tipo,
        string Referencia,
        decimal Entrada,
        decimal Salida,
        decimal Ajuste,
        decimal Saldo,
        decimal? CostoUnitario,
        decimal? CostoTotal,
        string? Documento,
        string? Proveedor,
        string? CreadoPor);

    private sealed record KardexLineaReporte(
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

    private sealed record CuentaPorPagarReporte(
        DateOnly Fecha,
        DateOnly FechaVencimiento,
        int DiasCredito,
        string NumeroDocumento,
        string Hotel,
        string Proveedor,
        string Estado,
        decimal Bruto,
        decimal Retencion,
        decimal NetoAPagar,
        decimal Pagado,
        decimal Saldo,
        List<PagoProveedorReporte> Pagos);

    private sealed record PagoProveedorReporte(
        DateOnly Fecha,
        decimal Monto,
        string MetodoPago,
        string? Referencia,
        string? Observaciones,
        string? CreadoPor,
        DateTime CreadoEn);

    private sealed record ConteoReporte(
        int Id,
        DateOnly Fecha,
        string Hotel,
        string Estado,
        string? Observaciones,
        DateTime CreadoEn,
        string? CreadoPor,
        DateTime? AjustesAplicadosEn,
        string? AjustesAplicadosPor,
        List<ConteoDetalleReporte> Detalles);

    private sealed record ConteoDetalleReporte(
        int ConteoId,
        DateOnly Fecha,
        string Hotel,
        string Estado,
        string? Observaciones,
        string Producto,
        string Categoria,
        string UnidadBase,
        decimal CantidadSistemaBase,
        decimal CantidadFisicaBase,
        decimal DiferenciaBase,
        decimal ValorDiferenciaEstimado,
        int? MovimientoAjusteId);

    private sealed record CierreMensualReporte(
        int Id,
        int HotelId,
        string Hotel,
        int Anio,
        int Mes,
        string Estado,
        decimal ComprasTotal,
        int DocumentosCompra,
        decimal ValorInventarioEstimado,
        int ProductosEnRiesgo,
        decimal ValorFaltanteEstimado,
        decimal ValorMermasEstimado,
        int MovimientosMerma,
        decimal ValorAjustesEstimado,
        int MovimientosAjuste,
        int ConteosFisicos,
        decimal ValorDiferenciasConteo,
        decimal SaldoCuentasPorPagar,
        decimal SaldoCuentasVencido,
        int DocumentosVencidos,
        DateTime FechaCierre,
        string? Observaciones,
        string? CreadoPor);
}
