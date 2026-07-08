using StockControl.Domain.Entities;

namespace StockControl.Application.Tests;

/// <summary>La normalización a unidad base es el cimiento de todas las métricas.</summary>
public class DetalleCompraTests
{
    [Fact]
    public void Total_EsCantidadPorPrecio()
    {
        var linea = new DetalleCompra { Cantidad = 10, PrecioUnitario = 6.5m };
        Assert.Equal(65m, linea.Total);
    }

    [Fact]
    public void CompraEnCajas_SeNormalizaALibras()
    {
        // 2 cajas a Q150 la caja, 1 caja = 25 lb → 50 lb a Q6/lb.
        var linea = new DetalleCompra { Cantidad = 2, PrecioUnitario = 150m, FactorABase = 25m };

        Assert.Equal(50m, linea.CantidadBase);
        Assert.Equal(6m, linea.PrecioPorUnidadBase);
        Assert.Equal(300m, linea.Total);
    }

    [Fact]
    public void FactorCero_NoRevienta()
    {
        var linea = new DetalleCompra { Cantidad = 5, PrecioUnitario = 10m, FactorABase = 0m };
        Assert.Equal(0m, linea.PrecioPorUnidadBase);
    }
}
