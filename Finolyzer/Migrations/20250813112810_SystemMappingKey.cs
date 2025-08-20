using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finolyzer.Migrations
{
    /// <inheritdoc />
    public partial class SystemMappingKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemMappingKey",
                table: "IntegrationServices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropColumn(
                name: "SystemMappingKey",
                table: "IntegrationServices");

        }
    }
}
