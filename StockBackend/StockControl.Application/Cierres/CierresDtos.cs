namespace StockControl.Application.Cierres;

public record FiltroCierresMensuales(int? HotelId, int? Anio, int? Mes);

public record CerrarMesRequest(int HotelId, int Anio, int Mes, string? Observaciones);

public record AnularCierreMensualRequest(string? Motivo);

public record CierreMensualDto(
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
    DateTime CreadoEn,
    string? CreadoPor);
