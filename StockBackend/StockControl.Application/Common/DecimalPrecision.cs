using System.Globalization;

namespace StockControl.Application.Common;

public static class DecimalPrecision
{
    public const int OperationalScale = 4;

    public static void ValidarEscalaOperativa(decimal valor, string campo) =>
        ValidarEscalaMaxima(valor, OperationalScale, campo);

    public static void ValidarEscalaMaxima(decimal valor, int decimales, string campo)
    {
        if (EscalaSignificativa(valor) > decimales)
            throw new InvalidOperationException($"{campo} permite maximo {decimales} decimales.");
    }

    private static int EscalaSignificativa(decimal valor)
    {
        var normalizado = decimal.Parse(valor.ToString("G29", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return (decimal.GetBits(normalizado)[3] >> 16) & 0xFF;
    }
}
