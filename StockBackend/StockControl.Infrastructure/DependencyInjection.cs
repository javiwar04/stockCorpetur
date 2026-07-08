using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StockControl.Application.Common.Interfaces;
using StockControl.Application.Importacion;
using StockControl.Application.Reportes;
using StockControl.Infrastructure.Identity;
using StockControl.Infrastructure.Importacion;
using StockControl.Infrastructure.Persistence;
using StockControl.Infrastructure.Reportes;

namespace StockControl.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // QuestPDF: licencia Community gratuita.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Importacion y reportes.
        services.AddScoped<IImportadorExcelService, ImportadorExcelService>();
        services.AddScoped<IReporteService, ReporteService>();

        // Persistencia.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<AuditableEntityInterceptor>();

        var conn = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("Falta la cadena de conexion 'Default'.");

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseSqlServer(conn);
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Identity.
        services.AddIdentity<ApplicationUser, IdentityRole>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = true;
                o.User.RequireUniqueEmail = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // JWT.
        services.Configure<JwtSettings>(config.GetSection(JwtSettings.Seccion));
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        var jwt = config.GetSection(JwtSettings.Seccion).Get<JwtSettings>()
                  ?? throw new InvalidOperationException("Falta la seccion de configuracion 'Jwt'.");
        if (string.IsNullOrWhiteSpace(jwt.Issuer))
            throw new InvalidOperationException("Falta configurar 'Jwt:Issuer'.");
        if (string.IsNullOrWhiteSpace(jwt.Audience))
            throw new InvalidOperationException("Falta configurar 'Jwt:Audience'.");
        if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
            throw new InvalidOperationException("Falta configurar 'Jwt:Key' con una clave fuerte de al menos 32 bytes.");

        services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
