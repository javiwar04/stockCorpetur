using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNumeroPedidoDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumeroPedido",
                table: "Documentos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE Documentos
                SET NumeroPedido = NumeroDocumento
                WHERE NumeroPedido IS NULL OR LTRIM(RTRIM(NumeroPedido)) = ''
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NumeroPedido",
                table: "Documentos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumeroPedido",
                table: "Documentos");
        }
    }
}
