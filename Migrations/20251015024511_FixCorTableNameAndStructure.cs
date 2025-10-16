using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cafApi.Migrations
{
    /// <inheritdoc />
    public partial class FixCorTableNameAndStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Cores",
                table: "Cores");

            migrationBuilder.RenameTable(
                name: "Cores",
                newName: "Cor");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Cor",
                table: "Cor",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Cor",
                table: "Cor");

            migrationBuilder.RenameTable(
                name: "Cor",
                newName: "Cores");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Cores",
                table: "Cores",
                column: "Id");
        }
    }
}
