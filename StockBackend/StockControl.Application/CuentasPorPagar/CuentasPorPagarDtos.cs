namespace StockControl.Application.CuentasPorPagar;

public record FiltroCuentasPorPagar(
    int? HotelId,
    int? ProveedorId,
    DateOnly? Desde,
    DateOnly? Hasta,
    bool SoloPendientes = true);

public record PagoProveedorDto(
    int Id,
    int DocumentoCompraId,
    int ProveedorId,
    string ProveedorNombre,
    DateOnly Fecha,
    decimal Monto,
    string MetodoPago,
    string? Referencia,
    string? Observaciones,
    DateTime CreadoEn,
    string? CreadoPor);

public record CuentaPorPagarDto(
    int DocumentoCompraId,
    DateOnly Fecha,
    DateOnly FechaVencimiento,
    string NumeroDocumento,
    int HotelId,
    string HotelNombre,
    int ProveedorId,
    string ProveedorNombre,
    int DiasCredito,
    decimal Bruto,
    decimal Retencion,
    decimal NetoAPagar,
    decimal Pagado,
    decimal Saldo,
    string Estado,
    List<PagoProveedorDto> Pagos);

public record ResumenCuentasPorPagarDto(
    decimal NetoAPagar,
    decimal Pagado,
    decimal SaldoPendiente,
    decimal SaldoVencido,
    int DocumentosPendientes,
    int DocumentosVencidos,
    decimal PorVencer,
    decimal Vencido0A30,
    decimal Vencido31A60,
    decimal Vencido61Mas);

public record CuentasPorPagarResultadoDto(
    ResumenCuentasPorPagarDto Resumen,
    List<CuentaPorPagarDto> Cuentas);

public record RegistrarPagoProveedorRequest(
    int DocumentoCompraId,
    DateOnly Fecha,
    decimal Monto,
    string MetodoPago,
    string? Referencia,
    string? Observaciones);
