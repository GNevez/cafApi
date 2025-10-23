using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cafApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCupomToCarrinhoAndPedidoDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DescontoCupom",
                table: "Pedidos",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DescontoPorUnidade",
                table: "Pedidos",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CupomId",
                table: "Carrinhos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Carrinhos_CupomId",
                table: "Carrinhos",
                column: "CupomId");

            migrationBuilder.AddForeignKey(
                name: "FK_Carrinhos_cupons_CupomId",
                table: "Carrinhos",
                column: "CupomId",
                principalTable: "cupons",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Carrinhos_cupons_CupomId",
                table: "Carrinhos");

            migrationBuilder.DropIndex(
                name: "IX_Carrinhos_CupomId",
                table: "Carrinhos");

            migrationBuilder.DropColumn(
                name: "DescontoCupom",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DescontoPorUnidade",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "CupomId",
                table: "Carrinhos");
        }
    }
}
