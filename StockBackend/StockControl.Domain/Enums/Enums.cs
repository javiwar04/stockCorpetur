namespace StockControl.Domain.Enums;

/// <summary>Categorías de producto tal como se manejan en el reporte de hoteles.</summary>
public enum CategoriaProducto
{
    Verdura = 1,
    Fruta = 2,
    Condimento = 3,
    Lacteo = 4,
    Proteina = 5,
    Otros = 6
}

/// <summary>
/// Tipo de movimiento de inventario. En Fase 1 solo se usa <see cref="Entrada"/>
/// (generado por una compra); Fase 2 activa Salida/Merma/Ajuste.
/// </summary>
public enum TipoMovimiento
{
    Entrada = 1,
    Salida = 2,
    Merma = 3,
    Ajuste = 4
}

/// <summary>Estado operativo de una compra dentro del flujo de recepcion.</summary>
public enum EstadoDocumentoCompra
{
    Borrador = 1,
    Recibido = 2,
    Anulado = 3
}

/// <summary>Clasificacion gerencial de una compra segun su planificacion.</summary>
public enum TipoCompra
{
    Ordinaria = 1,
    Extraordinaria = 2
}

/// <summary>Estado operativo de un conteo fisico de inventario.</summary>
public enum EstadoConteoInventario
{
    Registrado = 1,
    Ajustado = 2,
    Anulado = 3
}

/// <summary>Estado de un cierre mensual guardado como snapshot gerencial.</summary>
public enum EstadoCierreMensual
{
    Cerrado = 1,
    Anulado = 2
}
