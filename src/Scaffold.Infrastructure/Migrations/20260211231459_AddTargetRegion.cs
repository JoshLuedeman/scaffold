using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scaffold.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetRegion",
                table: "MigrationPlans",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetRegion",
                table: "MigrationPlans");
        }
    }
}
