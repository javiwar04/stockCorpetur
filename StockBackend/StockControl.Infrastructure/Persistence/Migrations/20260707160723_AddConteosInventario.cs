using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConteosInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConteosInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AjustesAplicadosEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AjustesAplicadosPor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModificadoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteosInventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConteosInventario_Hoteles_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hoteles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConteosInventarioDetalle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConteoInventarioId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    CantidadSistemaBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CantidadFisicaBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DiferenciaBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ValorDiferenciaEstimado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MovimientoAjusteId = table.Column<int>(type: "int", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModificadoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModificadoPor = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteosInventarioDetalle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConteosInventarioDetalle_ConteosInventario_ConteoInventarioId",
                        column: x => x.ConteoInventarioId,
                        principalTable: "ConteosInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConteosInventarioDetalle_Movimientos_MovimientoAjusteId",
                        column: x => x.MovimientoAjusteId,
                        principalTable: "Movimientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConteosInventarioDetalle_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConteosInventario_HotelId_Fecha",
                table: "ConteosInventario",
                columns: new[] { "HotelId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_ConteosInventarioDetalle_ConteoInventarioId_ProductoId",
                table: "ConteosInventarioDetalle",
                columns: new[] { "ConteoInventarioId", "ProductoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConteosInventarioDetalle_MovimientoAjusteId",
                table: "ConteosInventarioDetalle",
                column: "MovimientoAjusteId");

            migrationBuilder.CreateIndex(
                name: "IX_ConteosInventarioDetalle_ProductoId",
                table: "ConteosInventarioDetalle",
                column: "ProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConteosInventarioDetalle");

            migrationBuilder.DropTable(
                name: "ConteosInventario");
        }
    }
}
