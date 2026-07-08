namespace StockControl.Application.Catalogos;

public record ProveedorDto(int Id, string Nombre, string? Nit, string? Telefono, int DiasCredito, bool Activo);

public record CrearProveedorRequest(string Nombre, string? Nit, string? Telefono, int DiasCredito = 0);

public record ActualizarProveedorRequest(string Nombre, string? Nit, string? Telefono, int DiasCredito, bool Activo);
