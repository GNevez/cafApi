using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cafApi.Migrations
{
    /// <inheritdoc />
    public partial class PopulateCodigoPedidoForExistingRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Popular CodigoPedido com UUID para registros existentes que estão vazios
            migrationBuilder.Sql(@"
                UPDATE Pedidos 
                SET CodigoPedido = UUID() 
                WHERE CodigoPedido = '' OR CodigoPedido IS NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não há necessidade de reverter, pois os UUIDs são válidos
        }
    }
}
