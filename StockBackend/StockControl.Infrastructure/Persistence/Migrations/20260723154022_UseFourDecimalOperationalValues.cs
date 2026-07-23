using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockControl.Infrastructure.Persistence;

#nullable disable

namespace StockControl.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260723154022_UseFourDecimalOperationalValues")]
public partial class UseFourDecimalOperationalValues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AlterarPrecision(migrationBuilder, "ConteosInventarioDetalle", "ValorDiferenciaEstimado", 4, 2);

        AlterarPrecision(migrationBuilder, "CierresMensuales", "ComprasTotal", 4, 2);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "ValorInventarioEstimado", 4, 2);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "ValorFaltanteEstimado", 4, 2);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "ValorMermasEstimado", 4, 2);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "ValorAjustesEstimado", 4, 2);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "ValorDiferenciasConteo", 4, 2);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "SaldoCuentasPorPagar", 4, 2);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "SaldoCuentasVencido", 4, 2);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        AlterarPrecision(migrationBuilder, "ConteosInventarioDetalle", "ValorDiferenciaEstimado", 2, 4);

        AlterarPrecision(migrationBuilder, "CierresMensuales", "ComprasTotal", 2, 4);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "ValorInventarioEstimado", 2, 4);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "ValorFaltanteEstimado", 2, 4);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "ValorMermasEstimado", 2, 4);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "ValorAjustesEstimado", 2, 4);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "ValorDiferenciasConteo", 2, 4);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "SaldoCuentasPorPagar", 2, 4);
        AlterarPrecision(migrationBuilder, "CierresMensuales", "SaldoCuentasVencido", 2, 4);
    }

    private static void AlterarPrecision(MigrationBuilder migrationBuilder, string tabla, string columna, int escala, int escalaAnterior)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: columna,
            table: tabla,
            type: $"decimal(18,{escala})",
            precision: 18,
            scale: escala,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: $"decimal(18,{escalaAnterior})",
            oldPrecision: 18,
            oldScale: escalaAnterior);
    }
}
