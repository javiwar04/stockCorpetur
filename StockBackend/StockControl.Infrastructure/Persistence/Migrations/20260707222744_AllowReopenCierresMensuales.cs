using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowReopenCierresMensuales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CierresMensuales_HotelId_Anio_Mes",
                table: "CierresMensuales");

            migrationBuilder.CreateIndex(
                name: "IX_CierresMensuales_HotelId_Anio_Mes",
                table: "CierresMensuales",
                columns: new[] { "HotelId", "Anio", "Mes" },
                unique: true,
                filter: "[Estado] = 'Cerrado'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CierresMensuales_HotelId_Anio_Mes",
                table: "CierresMensuales");

            migrationBuilder.CreateIndex(
                name: "IX_CierresMensuales_HotelId_Anio_Mes",
                table: "CierresMensuales",
                columns: new[] { "HotelId", "Anio", "Mes" },
                unique: true);
        }
    }
}
