namespace StockControl.Domain.Common;

/// <summary>Roles del sistema. Usados en <c>[Authorize(Roles = ...)]</c> y en el seed.</summary>
public static class RolesApp
{
    /// <summary>Acceso total + gestión de usuarios.</summary>
    public const string Admin = "Admin";

    /// <summary>Dashboards y reportes de los 5 hoteles; sin gestión de usuarios.</summary>
    public const string Gerencia = "Gerencia";

    /// <summary>Registra documentos de compra, limitado a su(s) hotel(es).</summary>
    public const string Digitador = "Digitador";

    /// <summary>Consulta datos de sus hoteles asignados sin permisos de escritura.</summary>
    public const string SoloLectura = "SoloLectura";

    public const string EscrituraOperativa = Admin + "," + Gerencia + "," + Digitador;

    public static readonly string[] Todos = { Admin, Gerencia, Digitador, SoloLectura };

    public static bool UsaRestriccionHoteles(string rol) =>
        rol is Digitador or SoloLectura;
}
