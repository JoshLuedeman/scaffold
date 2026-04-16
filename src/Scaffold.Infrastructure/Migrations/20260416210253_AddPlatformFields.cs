using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scaffold.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourcePlatform",
                table: "MigrationPlans",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "SqlServer");

            migrationBuilder.AddColumn<string>(
                name: "TargetPlatform",
                table: "MigrationPlans",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "SqlServer");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "MigrationPlans",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Platform",
                table: "ConnectionInfos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "SqlServer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourcePlatform",
                table: "MigrationPlans");

            migrationBuilder.DropColumn(
                name: "TargetPlatform",
                table: "MigrationPlans");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MigrationPlans");

            migrationBuilder.DropColumn(
                name: "Platform",
                table: "ConnectionInfos");
        }
    }
}
