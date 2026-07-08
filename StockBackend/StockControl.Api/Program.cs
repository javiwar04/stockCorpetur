using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using StockControl.Api.Middleware;
using StockControl.Application;
using StockControl.Infrastructure;
using StockControl.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

const string CorsSpa = "spa";

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();

// Necesario cuando la API corre detras de Nginx o Cloudflare.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// CORS restringido al origen de la SPA (configurable por appsettings).
var origenesSpa = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>()
                  ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddPolicy(CorsSpa, p =>
    p.WithOrigins(origenesSpa).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// Rate limiting: protege el login de fuerza bruta.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("login", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors(CorsSpa);
app.UseRateLimiter();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

// Migra y siembra la BD al arrancar.
await DbInitializer.InicializarAsync(app.Services);

app.Run();
