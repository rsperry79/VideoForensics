using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ActorType = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PreviousEntryHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EntryHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAnalysisSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonDetected = table.Column<bool>(type: "INTEGER", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "TEXT", nullable: true),
                    FullDescription = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: true),
                    MotionZonesJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAnalysisSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Annotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Annotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CredentialType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EncryptedValue = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptionProvider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RotatedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceConfigSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MotionDetectionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    MotionSensitivity = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RecordingMode = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CustomSettingsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceConfigSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceHealthSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Connected = table.Column<bool>(type: "INTEGER", nullable: true),
                    BatteryPercentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    Rssi = table.Column<int>(type: "INTEGER", nullable: true),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceHealthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderDeviceId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    LastSuccessfulPullAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastPullAttemptAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderEventId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Answered = table.Column<bool>(type: "INTEGER", nullable: false),
                    Favorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    EventOccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecordingStatus = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DownloadStartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DownloadCompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderEventId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SnapshotUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportRecordItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExportRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaItemSha256HashAtExport = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportRecordItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExportedByUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CaseReference = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RecipientDescription = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ArchiveFileName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ArchiveSha256Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    WasEncrypted = table.Column<bool>(type: "INTEGER", nullable: false),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrityRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sha256Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Passed = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    VerifiedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrityRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadEventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    MediaFormat = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DownloadedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Sha256Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    VideoCodec = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AudioCodec = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Resolution = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    FrameRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    IntegrityVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastVerifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsPurged = table.Column<bool>(type: "INTEGER", nullable: false),
                    PurgedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PurgeReason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    LinkedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSuccessfulAuthUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderReconciliationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RanAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProviderEventId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DiscrepancyType = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    StoredValue = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ProviderValue = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderReconciliationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderUserKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionLogEntries_EntityType_EntityId",
                table: "ActionLogEntries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionLogEntries_TimestampUtc",
                table: "ActionLogEntries",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisSnapshots_DownloadEventId",
                table: "AiAnalysisSnapshots",
                column: "DownloadEventId");

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_EntityType_EntityId",
                table: "Annotations",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_Key_Value",
                table: "Annotations",
                columns: new[] { "Key", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_ProviderAccountId",
                table: "Credentials",
                column: "ProviderAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_ProviderAccountId_CredentialType",
                table: "Credentials",
                columns: new[] { "ProviderAccountId", "CredentialType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceConfigSnapshots_DeviceId",
                table: "DeviceConfigSnapshots",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthSnapshots_DownloadEventId",
                table: "DeviceHealthSnapshots",
                column: "DownloadEventId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_LocationId",
                table: "Devices",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_LocationId_ProviderDeviceId",
                table: "Devices",
                columns: new[] { "LocationId", "ProviderDeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DownloadEvents_DeviceId",
                table: "DownloadEvents",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadEvents_DeviceId_ProviderEventId",
                table: "DownloadEvents",
                columns: new[] { "DeviceId", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_DeviceId",
                table: "Events",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_DeviceId_ProviderEventId",
                table: "Events",
                columns: new[] { "DeviceId", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportRecordItems_ExportRecordId",
                table: "ExportRecordItems",
                column: "ExportRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportRecordItems_MediaItemId",
                table: "ExportRecordItems",
                column: "MediaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportRecords_ExportedAtUtc",
                table: "ExportRecords",
                column: "ExportedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrityRecords_MediaItemId",
                table: "IntegrityRecords",
                column: "MediaItemId");

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
                name: "IX_MediaItems_DeviceId",
                table: "MediaItems",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_DownloadEventId",
                table: "MediaItems",
                column: "DownloadEventId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_Sha256Hash",
                table: "MediaItems",
                column: "Sha256Hash");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAccounts_UserId",
                table: "ProviderAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAccounts_UserId_ProviderName",
                table: "ProviderAccounts",
                columns: new[] { "UserId", "ProviderName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderReconciliationRecords_DeviceId",
                table: "ProviderReconciliationRecords",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ProviderUserKey",
                table: "Users",
                column: "ProviderUserKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionLogEntries");

            migrationBuilder.DropTable(
                name: "AiAnalysisSnapshots");

            migrationBuilder.DropTable(
                name: "Annotations");

            migrationBuilder.DropTable(
                name: "Credentials");

            migrationBuilder.DropTable(
                name: "DeviceConfigSnapshots");

            migrationBuilder.DropTable(
                name: "DeviceHealthSnapshots");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "DownloadEvents");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "ExportRecordItems");

            migrationBuilder.DropTable(
                name: "ExportRecords");

            migrationBuilder.DropTable(
                name: "IntegrityRecords");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "MediaItems");

            migrationBuilder.DropTable(
                name: "ProviderAccounts");

            migrationBuilder.DropTable(
                name: "ProviderReconciliationRecords");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
