using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Add_MediaItemMetadataNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Annotations");

            migrationBuilder.DropTable(
                name: "DeviceHealthRecords");

            migrationBuilder.DropTable(
                name: "ModificationAuditRecords");

            migrationBuilder.DropTable(
                name: "RedactionAuditRecords");

            migrationBuilder.DropIndex(
                name: "IX_Locations_ProviderAccountId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_ProviderAccountId_ProviderLocationId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ProviderAccountId",
                table: "Locations");

            migrationBuilder.AddColumn<Guid>(
                name: "MediaItemDetectionId",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DetectedPersons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProfileId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ProfileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Confidence = table.Column<decimal>(type: "TEXT", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetectedPersons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DetectionTypeOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaItemDetectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DetectionType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetectionTypeOccurrences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DetectionZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaItemDetectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ZoneId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ZoneName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Confidence = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetectionZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaItemDetections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaItemId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_MediaItemDetections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AlertText = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAlerts", x => x.Id);
                });

            // Clean up duplicate ProviderLocationId values before creating unique constraint
            migrationBuilder.Sql(
                """
                DELETE FROM Locations
                WHERE rowid NOT IN (
                    SELECT MIN(rowid) FROM Locations
                    GROUP BY ProviderLocationId
                )
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ProviderLocationId",
                table: "Locations",
                column: "ProviderLocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetectedPersons_MediaItemId_ProfileId",
                table: "DetectedPersons",
                columns: new[] { "MediaItemId", "ProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_DetectionTypeOccurrences_MediaItemDetectionId",
                table: "DetectionTypeOccurrences",
                column: "MediaItemDetectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DetectionZones_MediaItemDetectionId_ZoneId",
                table: "DetectionZones",
                columns: new[] { "MediaItemDetectionId", "ZoneId" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItemDetections_MediaItemId",
                table: "MediaItemDetections",
                column: "MediaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAlerts_MediaItemId",
                table: "SecurityAlerts",
                column: "MediaItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetectedPersons");

            migrationBuilder.DropTable(
                name: "DetectionTypeOccurrences");

            migrationBuilder.DropTable(
                name: "DetectionZones");

            migrationBuilder.DropTable(
                name: "MediaItemDetections");

            migrationBuilder.DropTable(
                name: "SecurityAlerts");

            migrationBuilder.DropIndex(
                name: "IX_Locations_ProviderLocationId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "MediaItemDetectionId",
                table: "MediaItems");

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderAccountId",
                table: "Locations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Annotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Annotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceHealthRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApiResponseHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    BatteryPercentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    WifiSignalRssi = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceHealthRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModificationAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApprovedByInvestigator = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChangeSummary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModificationType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModificationAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RedactionAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ContentRedacted = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EvidenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JustificationNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RedactedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RedactedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedactionAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ProviderAccountId",
                table: "Locations",
                column: "ProviderAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ProviderAccountId_ProviderLocationId",
                table: "Locations",
                columns: new[] { "ProviderAccountId", "ProviderLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_EntityType_EntityId",
                table: "Annotations",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_Key_Value",
                table: "Annotations",
                columns: new[] { "Key", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthRecords_DeviceId",
                table: "DeviceHealthRecords",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModificationAuditRecords_EventId",
                table: "ModificationAuditRecords",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ModificationAuditRecords_ModifiedAtUtc",
                table: "ModificationAuditRecords",
                column: "ModifiedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RedactionAuditRecords_EvidenceId",
                table: "RedactionAuditRecords",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_RedactionAuditRecords_RedactedAtUtc",
                table: "RedactionAuditRecords",
                column: "RedactedAtUtc");
        }
    }
}
