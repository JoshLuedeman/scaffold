using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scaffold.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationScripts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PreMigrationScript",
                table: "MigrationPlans",
                newName: "PreMigrationScriptsJson");

            migrationBuilder.RenameColumn(
                name: "PostMigrationScript",
                table: "MigrationPlans",
                newName: "PostMigrationScriptsJson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PreMigrationScriptsJson",
                table: "MigrationPlans",
                newName: "PreMigrationScript");

            migrationBuilder.RenameColumn(
                name: "PostMigrationScriptsJson",
                table: "MigrationPlans",
                newName: "PostMigrationScript");
        }
    }
}
