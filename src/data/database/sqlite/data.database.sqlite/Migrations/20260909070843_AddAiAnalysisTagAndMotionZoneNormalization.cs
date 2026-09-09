using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAnalysisTagAndMotionZoneNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotionZonesJson",
                table: "AiAnalysisSnapshots");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "AiAnalysisSnapshots");

            migrationBuilder.CreateTable(
                name: "AiAnalysisMotionZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAnalysisSnapshotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ZoneId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ZoneName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Confidence = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAnalysisMotionZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAnalysisMotionZones_AiAnalysisSnapshots_AiAnalysisSnapshotId",
                        column: x => x.AiAnalysisSnapshotId,
                        principalTable: "AiAnalysisSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiAnalysisTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAnalysisSnapshotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAnalysisTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAnalysisTags_AiAnalysisSnapshots_AiAnalysisSnapshotId",
                        column: x => x.AiAnalysisSnapshotId,
                        principalTable: "AiAnalysisSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisMotionZones_AiAnalysisSnapshotId",
                table: "AiAnalysisMotionZones",
                column: "AiAnalysisSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisMotionZones_AiAnalysisSnapshotId_ZoneId",
                table: "AiAnalysisMotionZones",
                columns: new[] { "AiAnalysisSnapshotId", "ZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisTags_AiAnalysisSnapshotId",
                table: "AiAnalysisTags",
                column: "AiAnalysisSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisTags_AiAnalysisSnapshotId_TagName",
                table: "AiAnalysisTags",
                columns: new[] { "AiAnalysisSnapshotId", "TagName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAnalysisMotionZones");

            migrationBuilder.DropTable(
                name: "AiAnalysisTags");

            migrationBuilder.AddColumn<string>(
                name: "MotionZonesJson",
                table: "AiAnalysisSnapshots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "AiAnalysisSnapshots",
                type: "TEXT",
                nullable: true);
        }
    }
}
