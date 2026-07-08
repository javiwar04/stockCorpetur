namespace StockControl.Infrastructure.Identity;

/// <summary>Configuración del JWT, enlazada desde la sección "Jwt" de appsettings.</summary>
public class JwtSettings
{
    public const string Seccion = "Jwt";

    public string Issuer { get; set; } = "StockControl";
    public string Audience { get; set; } = "StockControlClient";

    /// <summary>Clave simétrica de firma. NUNCA en el repo: user-secrets o variable de entorno.</summary>
    public string Key { get; set; } = null!;

    public int AccessTokenMinutos { get; set; } = 60;
    public int RefreshTokenDias { get; set; } = 7;
}

/// <summary>Tipos de claim personalizados usados por la app.</summary>
public static class ClaimsApp
{
    /// <summary>Un claim por cada hotel al que el usuario tiene acceso.</summary>
    public const string Hotel = "hotel";
}
