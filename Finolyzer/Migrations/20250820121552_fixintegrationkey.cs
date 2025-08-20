using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finolyzer.Migrations
{
    /// <inheritdoc />
    public partial class fixintegrationkey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationIntegrationKeys_ApplicationSystems_ApplicationSystemId1",
                table: "ApplicationIntegrationKeys");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationIntegrationKeys_ApplicationSystemId1",
                table: "ApplicationIntegrationKeys");

            migrationBuilder.DropColumn(
                name: "ApplicationSystemId1",
                table: "ApplicationIntegrationKeys");

            migrationBuilder.CreateTable(
                name: "AbpAuditLogExcelFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbpAuditLogExcelFiles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbpAuditLogExcelFiles");


            migrationBuilder.AddColumn<int>(
                name: "ApplicationSystemId1",
                table: "ApplicationIntegrationKeys",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationIntegrationKeys_ApplicationSystemId1",
                table: "ApplicationIntegrationKeys",
                column: "ApplicationSystemId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationIntegrationKeys_ApplicationSystems_ApplicationSystemId1",
                table: "ApplicationIntegrationKeys",
                column: "ApplicationSystemId1",
                principalTable: "ApplicationSystems",
                principalColumn: "Id");
        }
    }
}
