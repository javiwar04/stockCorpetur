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

    public static readonly string[] Todos = { Admin, Gerencia, Digitador };
}
