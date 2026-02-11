using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scaffold.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MigrationProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompatibilityScore = table.Column<double>(type: "float", nullable: false),
                    Risk = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompatibilityIssues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataProfile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Performance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Schema = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentReports_MigrationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "MigrationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Server = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Database = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    UseSqlAuthentication = table.Column<bool>(type: "bit", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    KeyVaultSecretUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrustServerCertificate = table.Column<bool>(type: "bit", nullable: false),
                    MigrationProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectionInfos_MigrationProjects_MigrationProjectId",
                        column: x => x.MigrationProjectId,
                        principalTable: "MigrationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MigrationPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Strategy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IncludedObjects = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExcludedObjects = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreMigrationScript = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostMigrationScript = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UseExistingTarget = table.Column<bool>(type: "bit", nullable: false),
                    ExistingTargetConnectionString = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TargetTier = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationPlans_MigrationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "MigrationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MigrationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowsMigrated = table.Column<long>(type: "bigint", nullable: false),
                    DataSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Errors = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Validations = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationResults_MigrationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "MigrationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentReports_ProjectId",
                table: "AssessmentReports",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionInfos_MigrationProjectId",
                table: "ConnectionInfos",
                column: "MigrationProjectId",
                unique: true,
                filter: "[MigrationProjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationPlans_ProjectId",
                table: "MigrationPlans",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MigrationResults_ProjectId",
                table: "MigrationResults",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentReports");

            migrationBuilder.DropTable(
                name: "ConnectionInfos");

            migrationBuilder.DropTable(
                name: "MigrationPlans");

            migrationBuilder.DropTable(
                name: "MigrationResults");

            migrationBuilder.DropTable(
                name: "MigrationProjects");
        }
    }
}
