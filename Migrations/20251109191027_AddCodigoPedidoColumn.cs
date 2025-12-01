using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cafApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCodigoPedidoColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoPedido",
                table: "Pedidos",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoPedido",
                table: "Pedidos");
        }
    }
}
