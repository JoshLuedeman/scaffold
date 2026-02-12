using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scaffold.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRejected",
                table: "MigrationPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                table: "MigrationPlans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "MigrationPlans",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceConnectionString",
                table: "MigrationPlans",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRejected",
                table: "MigrationPlans");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                table: "MigrationPlans");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "MigrationPlans");

            migrationBuilder.DropColumn(
                name: "SourceConnectionString",
                table: "MigrationPlans");
        }
    }
}
