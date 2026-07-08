using Microsoft.Extensions.DependencyInjection;
using StockControl.Application.Auditoria;
using StockControl.Application.Alertas;
using StockControl.Application.Catalogos;
using StockControl.Application.Cierres;
using StockControl.Application.Compras;
using StockControl.Application.Conteos;
using StockControl.Application.CuentasPorPagar;
using StockControl.Application.Dashboard;
using StockControl.Application.Gestion;
using StockControl.Application.Inventario;
using StockControl.Application.Recetas;

namespace StockControl.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<IProveedorService, ProveedorService>();
        services.AddScoped<ICatalogoAuxiliarService, CatalogoAuxiliarService>();
        services.AddScoped<IAuditoriaService, AuditoriaService>();
        services.AddScoped<IAlertaService, AlertaService>();
        services.AddScoped<ICierreMensualGuard, CierreMensualGuard>();
        services.AddScoped<ICierreMensualService, CierreMensualService>();
        services.AddScoped<IDocumentoCompraService, DocumentoCompraService>();
        services.AddScoped<IConteoInventarioService, ConteoInventarioService>();
        services.AddScoped<ICuentasPorPagarService, CuentasPorPagarService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IGestionService, GestionService>();
        services.AddScoped<IInventarioService, InventarioService>();
        services.AddScoped<IRecetaService, RecetaService>();

        return services;
    }
}
