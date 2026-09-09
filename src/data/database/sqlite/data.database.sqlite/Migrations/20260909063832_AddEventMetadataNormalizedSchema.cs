using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddEventMetadataNormalizedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventDetectionId",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordingStatus",
                table: "Events",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventDetectedPersons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProfileId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ProfileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Confidence = table.Column<decimal>(type: "TEXT", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventDetectedPersons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventDetections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonDetected = table.Column<bool>(type: "INTEGER", nullable: true),
                    StreamBroken = table.Column<bool>(type: "INTEGER", nullable: true),
                    DetectionType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FullDescription = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ShortDescription = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Similarity = table.Column<decimal>(type: "TEXT", nullable: true),
                    Anomaly = table.Column<decimal>(type: "TEXT", nullable: true),
                    Confidence = table.Column<decimal>(type: "TEXT", nullable: true),
                    ModelVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventDetections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventDetectionTypeOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventDetectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DetectionType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventDetectionTypeOccurrences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventDetectionZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventDetectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ZoneId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ZoneName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Confidence = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventDetectionZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventSecurityAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AlertText = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSecurityAlerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventDetectionId",
                table: "Events",
                column: "EventDetectionId");

            migrationBuilder.CreateIndex(
                name: "IX_EventDetectedPersons_EventId",
                table: "EventDetectedPersons",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventDetections_EventId",
                table: "EventDetections",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventDetectionTypeOccurrences_EventDetectionId",
                table: "EventDetectionTypeOccurrences",
                column: "EventDetectionId");

            migrationBuilder.CreateIndex(
                name: "IX_EventDetectionZones_EventDetectionId_ZoneId",
                table: "EventDetectionZones",
                columns: new[] { "EventDetectionId", "ZoneId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventSecurityAlerts_EventId",
                table: "EventSecurityAlerts",
                column: "EventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventDetectedPersons");

            migrationBuilder.DropTable(
                name: "EventDetections");

            migrationBuilder.DropTable(
                name: "EventDetectionTypeOccurrences");

            migrationBuilder.DropTable(
                name: "EventDetectionZones");

            migrationBuilder.DropTable(
                name: "EventSecurityAlerts");

            migrationBuilder.DropIndex(
                name: "IX_Events_EventDetectionId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EventDetectionId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RecordingStatus",
                table: "Events");
        }
    }
}
