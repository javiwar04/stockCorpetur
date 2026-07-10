using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoCompraDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoCompra",
                table: "Documentos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Ordinaria");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoCompra",
                table: "Documentos");
        }
    }
}
