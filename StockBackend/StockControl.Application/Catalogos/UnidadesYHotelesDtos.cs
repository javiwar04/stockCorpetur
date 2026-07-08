namespace StockControl.Application.Catalogos;

public record UnidadDto(int Id, string Nombre, string Abreviatura);
public record CrearUnidadRequest(string Nombre, string Abreviatura);

public record HotelDto(int Id, string Nombre, bool Activo);
