using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cafApi.Migrations
{
    /// <inheritdoc />
    public partial class AddNomeClienteAndTelefoneCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NomeCliente",
                table: "Pedidos",
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TelefoneCliente",
                table: "Pedidos",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Preencher com dados existentes
            migrationBuilder.Sql(@"
                UPDATE Pedidos p
                INNER JOIN Clientes c ON p.ClienteId = c.Id
                SET p.NomeCliente = c.Nome, 
                    p.TelefoneCliente = c.Telefone
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NomeCliente",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "TelefoneCliente",
                table: "Pedidos");
        }
    }
}
