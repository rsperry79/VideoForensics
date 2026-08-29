using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RefreshSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ApiSourceHash",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DownloadedAtUtc",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventIntegrityHash",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AccessAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AccessedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceCapabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    HasAudio = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasNightVision = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasMotionDetection = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasCloudStorage = table.Column<bool>(type: "INTEGER", nullable: true),
                    StorageType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MaxStorageDays = table.Column<int>(type: "INTEGER", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    HardwareModel = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiResponseHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCapabilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceHealthRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatteryPercentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    WifiSignalRssi = table.Column<int>(type: "INTEGER", nullable: true),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiResponseHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceHealthRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExportedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EventsExported = table.Column<int>(type: "INTEGER", nullable: false),
                    ExportFormat = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocationMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StreetAddress = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiResponseHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModificationAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ModificationType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ChangeSummary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ApprovedByInvestigator = table.Column<bool>(type: "INTEGER", nullable: false)
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
                    EvidenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RedactedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RedactedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ContentRedacted = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    JustificationNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedactionAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionLevel = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Features = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RateLimitPerMinute = table.Column<int>(type: "INTEGER", nullable: true),
                    RateLimitRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    AccountEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AuthenticatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiResponseHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_AccessedAtUtc",
                table: "AccessAuditLogs",
                column: "AccessedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_EvidenceId",
                table: "AccessAuditLogs",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_UserId",
                table: "AccessAuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCapabilities_DeviceId",
                table: "DeviceCapabilities",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthRecords_DeviceId",
                table: "DeviceHealthRecords",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportAuditRecords_ExportedAtUtc",
                table: "ExportAuditRecords",
                column: "ExportedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExportAuditRecords_LocationId",
                table: "ExportAuditRecords",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationMetadata_LocationId",
                table: "LocationMetadata",
                column: "LocationId",
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

            migrationBuilder.CreateIndex(
                name: "IX_RingAccounts_ProviderAccountId",
                table: "RingAccounts",
                column: "ProviderAccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessAuditLogs");

            migrationBuilder.DropTable(
                name: "DeviceCapabilities");

            migrationBuilder.DropTable(
                name: "DeviceHealthRecords");

            migrationBuilder.DropTable(
                name: "ExportAuditRecords");

            migrationBuilder.DropTable(
                name: "LocationMetadata");

            migrationBuilder.DropTable(
                name: "ModificationAuditRecords");

            migrationBuilder.DropTable(
                name: "RedactionAuditRecords");

            migrationBuilder.DropTable(
                name: "RingAccounts");

            migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ApiSourceHash",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "DownloadedAtUtc",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EventIntegrityHash",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "Devices");
        }
    }
}
