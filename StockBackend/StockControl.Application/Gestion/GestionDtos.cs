namespace StockControl.Application.Gestion;

public record ComensalDto(int HotelId, string Hotel, int Anio, int Mes, int NumeroComensales);

public record UpsertComensalRequest(int HotelId, int Anio, int Mes, int NumeroComensales);

public record PresupuestoDto(int HotelId, string Hotel, string Categoria, int Anio, int Mes, decimal Monto);

public record UpsertPresupuestoRequest(int HotelId, string Categoria, int Anio, int Mes, decimal Monto);
