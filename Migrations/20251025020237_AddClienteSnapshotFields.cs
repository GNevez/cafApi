using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cafApi.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailCliente",
                table: "Pedidos",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NomeCliente",
                table: "Pedidos",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TelefoneCliente",
                table: "Pedidos",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailCliente",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "NomeCliente",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "TelefoneCliente",
                table: "Pedidos");
        }
    }
}
