using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelAndDiscountPerPurchaseLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Monto",
                table: "PagosProveedor",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Retencion",
                table: "Documentos",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "Descuento",
                table: "Detalles",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "HotelId",
                table: "Detalles",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE detalle
                SET HotelId = documento.HotelId
                FROM Detalles detalle
                INNER JOIN Documentos documento ON documento.Id = detalle.DocumentoCompraId
                WHERE detalle.HotelId IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "HotelId",
                table: "Detalles",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Detalles_HotelId_ProductoId",
                table: "Detalles",
                columns: new[] { "HotelId", "ProductoId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Detalles_Hoteles_HotelId",
                table: "Detalles",
                column: "HotelId",
                principalTable: "Hoteles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Detalles_Hoteles_HotelId",
                table: "Detalles");

            migrationBuilder.DropIndex(
                name: "IX_Detalles_HotelId_ProductoId",
                table: "Detalles");

            migrationBuilder.DropColumn(
                name: "Descuento",
                table: "Detalles");

            migrationBuilder.DropColumn(
                name: "HotelId",
                table: "Detalles");

            migrationBuilder.AlterColumn<decimal>(
                name: "Monto",
                table: "PagosProveedor",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "Retencion",
                table: "Documentos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);
        }
    }
}
