using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cafApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCodigoPedidoFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Atualizar todos os pedidos existentes com novo formato de código
            // Usa o ID como base + timestamp para garantir unicidade
            migrationBuilder.Sql(@"
                UPDATE Pedidos 
                SET CodigoPedido = CONCAT('CAF-', UNIX_TIMESTAMP(NOW()) * 1000 + Id, FLOOR(100 + RAND() * 899))
                WHERE CodigoPedido IS NULL OR CodigoPedido = '' OR LENGTH(CodigoPedido) > 20
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não há necessidade de reverter
        }
    }
}
