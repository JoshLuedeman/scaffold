using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scaffold.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationStatusAndProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MigrationId",
                table: "MigrationPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "MigrationPlans",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "MigrationProgressRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MigrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PercentComplete = table.Column<double>(type: "float", nullable: false),
                    CurrentTable = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RowsProcessed = table.Column<long>(type: "bigint", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationProgressRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MigrationProgressRecords_MigrationId",
                table: "MigrationProgressRecords",
                column: "MigrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MigrationProgressRecords");

            migrationBuilder.DropColumn(
                name: "MigrationId",
                table: "MigrationPlans");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "MigrationPlans");
        }
    }
}
