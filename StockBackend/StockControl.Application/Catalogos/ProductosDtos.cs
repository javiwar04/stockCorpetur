namespace StockControl.Application.Catalogos;

public record ProductoDto(
    int Id,
    string Nombre,
    string Categoria,
    bool Activo,
    int UnidadBaseId,
    string UnidadBaseNombre);

public record CrearProductoRequest(string Nombre, string Categoria, int UnidadBaseId);

public record ActualizarProductoRequest(string Nombre, string Categoria, int UnidadBaseId, bool Activo);

public record ConversionDto(int Id, int UnidadId, string UnidadNombre, decimal FactorABase);

public record CrearConversionRequest(int UnidadId, decimal FactorABase);
