namespace StockControl.Application.Importacion;

/// <summary>Resultado de importar un libro de reporte mensual (formato Excel de los hoteles).</summary>
public record ResultadoImportacion(
    int HojasProcesadas,
    int DocumentosCreados,
    int DocumentosOmitidos,
    int ProductosCreados,
    int LineasCreadas,
    List<string> HojasNoReconocidas,
    List<string> Advertencias);

/// <summary>
/// Importa el Excel histórico de compras (una hoja por hotel, formato pivote:
/// productos en filas, documentos en grupos de columnas Cantidad/Precio/Total).
/// </summary>
public interface IImportadorExcelService
{
    Task<ResultadoImportacion> ImportarAsync(Stream archivo, CancellationToken ct = default);
}
