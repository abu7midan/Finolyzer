using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finolyzer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "YearlyCost",
                table: "IntegrationServices");

            migrationBuilder.AddColumn<float>(
                name: "UnitCost",
                table: "IntegrationServices",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "IntegrationServices");

            migrationBuilder.AddColumn<float>(
                name: "YearlyCost",
                table: "IntegrationServices",
                type: "real",
                nullable: true);
        }
    }
}
