using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finolyzer.Migrations
{
    /// <inheritdoc />
    public partial class IntegrationKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationIntegrationKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrationServiceId = table.Column<int>(type: "int", nullable: false),
                    ApplicationSystemId = table.Column<int>(type: "int", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationIntegrationKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationIntegrationKeys_ApplicationSystems_ApplicationSystemId",
                        column: x => x.ApplicationSystemId,
                        principalTable: "ApplicationSystems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApplicationIntegrationKeys_IntegrationServices_IntegrationServiceId",
                        column: x => x.IntegrationServiceId,
                        principalTable: "IntegrationServices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationIntegrationKeys_ApplicationSystemId",
                table: "ApplicationIntegrationKeys",
                column: "ApplicationSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationIntegrationKeys_IntegrationServiceId",
                table: "ApplicationIntegrationKeys",
                column: "IntegrationServiceId");

            // Add foreign key from SystemIntegrationTransactions to ApplicationIntegrationKey
            migrationBuilder.AddColumn<int>(
                name: "ApplicationIntegrationKeyId",
                table: "SystemIntegrationTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SystemIntegrationTransactions_ApplicationIntegrationKeyId",
                table: "SystemIntegrationTransactions",
                column: "ApplicationIntegrationKeyId");

            migrationBuilder.AddForeignKey(
                name: "FK_SystemIntegrationTransactions_ApplicationIntegrationKeys_ApplicationIntegrationKeyId",
                table: "SystemIntegrationTransactions",
                column: "ApplicationIntegrationKeyId",
                principalTable: "ApplicationIntegrationKeys",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SystemIntegrationTransactions_ApplicationIntegrationKeys_ApplicationIntegrationKeyId",
                table: "SystemIntegrationTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SystemIntegrationTransactions_ApplicationIntegrationKeyId",
                table: "SystemIntegrationTransactions");

            migrationBuilder.DropColumn(
                name: "ApplicationIntegrationKeyId",
                table: "SystemIntegrationTransactions");

            migrationBuilder.DropTable(
                name: "ApplicationIntegrationKeys");
        }
    }
}
