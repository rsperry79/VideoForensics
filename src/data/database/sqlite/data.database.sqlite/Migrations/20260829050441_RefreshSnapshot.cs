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
            _ = migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            _ = migrationBuilder.AddColumn<string>(
                name: "ApiSourceHash",
                table: "Events",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<DateTime>(
                name: "DownloadedAtUtc",
                table: "Events",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "EventIntegrityHash",
                table: "Events",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_AccessAuditLogs", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceCapabilities", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceHealthRecords", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_ExportAuditRecords", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_LocationMetadata", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_ModificationAuditRecords", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_RedactionAuditRecords", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_RingAccounts", x => x.Id);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_AccessedAtUtc",
                table: "AccessAuditLogs",
                column: "AccessedAtUtc");

            _ = migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_EvidenceId",
                table: "AccessAuditLogs",
                column: "EvidenceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_UserId",
                table: "AccessAuditLogs",
                column: "UserId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceCapabilities_DeviceId",
                table: "DeviceCapabilities",
                column: "DeviceId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthRecords_DeviceId",
                table: "DeviceHealthRecords",
                column: "DeviceId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_ExportAuditRecords_ExportedAtUtc",
                table: "ExportAuditRecords",
                column: "ExportedAtUtc");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ExportAuditRecords_LocationId",
                table: "ExportAuditRecords",
                column: "LocationId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_LocationMetadata_LocationId",
                table: "LocationMetadata",
                column: "LocationId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_ModificationAuditRecords_EventId",
                table: "ModificationAuditRecords",
                column: "EventId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ModificationAuditRecords_ModifiedAtUtc",
                table: "ModificationAuditRecords",
                column: "ModifiedAtUtc");

            _ = migrationBuilder.CreateIndex(
                name: "IX_RedactionAuditRecords_EvidenceId",
                table: "RedactionAuditRecords",
                column: "EvidenceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_RedactionAuditRecords_RedactedAtUtc",
                table: "RedactionAuditRecords",
                column: "RedactedAtUtc");

            _ = migrationBuilder.CreateIndex(
                name: "IX_RingAccounts_ProviderAccountId",
                table: "RingAccounts",
                column: "ProviderAccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "AccessAuditLogs");

            _ = migrationBuilder.DropTable(
                name: "DeviceCapabilities");

            _ = migrationBuilder.DropTable(
                name: "DeviceHealthRecords");

            _ = migrationBuilder.DropTable(
                name: "ExportAuditRecords");

            _ = migrationBuilder.DropTable(
                name: "LocationMetadata");

            _ = migrationBuilder.DropTable(
                name: "ModificationAuditRecords");

            _ = migrationBuilder.DropTable(
                name: "RedactionAuditRecords");

            _ = migrationBuilder.DropTable(
                name: "RingAccounts");

            _ = migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Locations");

            _ = migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "Locations");

            _ = migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "Locations");

            _ = migrationBuilder.DropColumn(
                name: "ApiSourceHash",
                table: "Events");

            _ = migrationBuilder.DropColumn(
                name: "DownloadedAtUtc",
                table: "Events");

            _ = migrationBuilder.DropColumn(
                name: "EventIntegrityHash",
                table: "Events");

            _ = migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "Devices");
        }
    }
}
