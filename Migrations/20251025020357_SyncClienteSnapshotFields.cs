using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cafApi.Migrations
{
    /// <inheritdoc />
    public partial class SyncClienteSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Os campos EmailCliente, NomeCliente e TelefoneCliente já foram adicionados manualmente
            // Esta migration apenas sincroniza o snapshot do EF Core com o estado do banco
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverter removendo os campos snapshot
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
