using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cafApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPagarmeOrderIdToPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PagarmeOrderId",
                table: "Pedidos",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PagarmeOrderId",
                table: "Pedidos");
        }
    }
}
