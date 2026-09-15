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
                    _ = table.PrimaryKey("PK_ActionLogEntries", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "AiAnalysisSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonDetected = table.Column<bool>(type: "INTEGER", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "TEXT", nullable: true),
                    FullDescription = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_AiAnalysisSnapshots", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActiveProviderAccountId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_Credentials", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DetectedPersons", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DetectionTypeOccurrences", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "DeviceAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlertType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_DeviceAlerts", x => x.Id);
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
                    _ = table.PrimaryKey("PK_DeviceConfigSnapshots", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "DeviceFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MotionsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ShowRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    AdvancedMotionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    PeopleOnlyEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ShadowCorrectionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    MotionMessageEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    NightVisionEnabled = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_DeviceFeatures", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "DeviceHealths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatteryPercentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    BatteryVoltageValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    WifiSignalRssi = table.Column<int>(type: "INTEGER", nullable: true),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsExternalPowerConnected = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtaStatus = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_DeviceHealths", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "DeviceHealthSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DownloadEventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Connected = table.Column<bool>(type: "INTEGER", nullable: true),
                    BatteryPercentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    Rssi = table.Column<int>(type: "INTEGER", nullable: true),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_DeviceHealthSnapshots", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiResponseHash = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceHealthId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeviceFeaturesId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeviceLocationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_Devices", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DownloadEvents", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_EventDetectedPersons", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_EventDetections", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_EventDetectionTypeOccurrences", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_EventDetectionZones", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    DiscoveredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DownloadedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApiSourceHash = table.Column<string>(type: "TEXT", nullable: true),
                    EventIntegrityHash = table.Column<string>(type: "TEXT", nullable: true),
                    EventDetectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecordingStatus = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DownloadStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    DownloadFailedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_Events", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_EventSecurityAlerts", x => x.Id);
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
                    _ = table.PrimaryKey("PK_ExportRecordItems", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_ExportRecords", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_IntegrityRecords", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "JammingIncidentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AffectedEventCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageDegradationDb = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_JammingIncidentRecords", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "JammingStatsSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IncidentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalJammedDurationMinutes = table.Column<double>(type: "REAL", nullable: false),
                    AverageDegradationDb = table.Column<double>(type: "REAL", nullable: false),
                    MaxDegradationDb = table.Column<double>(type: "REAL", nullable: false),
                    LowConfidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MediumConfidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HighConfidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DefiniteConfidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstIncidentUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastIncidentUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_JammingStatsSummaries", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "LegalHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReleasedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReleaseReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_LegalHolds", x => x.Id);
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
                    IsOwner = table.Column<bool>(type: "INTEGER", nullable: true),
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
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiResponseHash = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_Locations", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_MediaItemDetections", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadEventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MediaItemDetectionId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                    PurgeReason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    ApiSourceHash = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_MediaItems", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "OperatorPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThemeMode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CultureName = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_OperatorPreferences", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "Operators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_Operators", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "PairedDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    WebAuthnCredentialId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    WebAuthnPublicKey = table.Column<byte[]>(type: "BLOB", nullable: true),
                    WebAuthnSignCount = table.Column<uint>(type: "INTEGER", nullable: false),
                    FallbackApiKeyHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PinnedCertificateFingerprint = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PairedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSeenIp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LastSeenTier = table.Column<int>(type: "INTEGER", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedReason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_PairedDevices", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "ProviderAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    LinkedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSuccessfulAuthUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastDownloadTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_ProviderAccounts", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "ProviderApiCallRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_ProviderApiCallRecords", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "ProviderApiErrorLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HttpMethod = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    RequestUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: true),
                    ExceptionType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ErrorCategory = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_ProviderApiErrorLogs", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_ProviderReconciliationRecords", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "RingAccountFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CanViewLiveView = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanViewRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanViewSnapshotsOnly = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanSaveLiveView = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanSaveRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanDeleteRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanPauseRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanShareRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanChangeDeviceSettings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanActivateAlarmSystem = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_RingAccountFeatures", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "RingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountFeaturesId = table.Column<Guid>(type: "TEXT", nullable: true),
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

            _ = migrationBuilder.CreateTable(
                name: "SecurityAuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PairedDeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceIp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    IsUrgent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_SecurityAuditLogEntries", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_Users", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_AiAnalysisMotionZones", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_AiAnalysisMotionZones_AiAnalysisSnapshots_AiAnalysisSnapshotId",
                        column: x => x.AiAnalysisSnapshotId,
                        principalTable: "AiAnalysisSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
                name: "AiAnalysisTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiAnalysisSnapshotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_AiAnalysisTags", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_AiAnalysisTags_AiAnalysisSnapshots_AiAnalysisSnapshotId",
                        column: x => x.AiAnalysisSnapshotId,
                        principalTable: "AiAnalysisSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_ActionLogEntries_EntityType_EntityId",
                table: "ActionLogEntries",
                columns: new[] { "EntityType", "EntityId" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ActionLogEntries_TimestampUtc",
                table: "ActionLogEntries",
                column: "TimestampUtc");

            _ = migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisMotionZones_AiAnalysisSnapshotId",
                table: "AiAnalysisMotionZones",
                column: "AiAnalysisSnapshotId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisMotionZones_AiAnalysisSnapshotId_ZoneId",
                table: "AiAnalysisMotionZones",
                columns: new[] { "AiAnalysisSnapshotId", "ZoneId" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisSnapshots_DownloadEventId",
                table: "AiAnalysisSnapshots",
                column: "DownloadEventId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisTags_AiAnalysisSnapshotId",
                table: "AiAnalysisTags",
                column: "AiAnalysisSnapshotId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisTags_AiAnalysisSnapshotId_TagName",
                table: "AiAnalysisTags",
                columns: new[] { "AiAnalysisSnapshotId", "TagName" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_AppSettings_Key",
                table: "AppSettings",
                column: "Key",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_Credentials_ProviderAccountId",
                table: "Credentials",
                column: "ProviderAccountId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Credentials_ProviderAccountId_CredentialType",
                table: "Credentials",
                columns: new[] { "ProviderAccountId", "CredentialType" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_DetectedPersons_MediaItemId_ProfileId",
                table: "DetectedPersons",
                columns: new[] { "MediaItemId", "ProfileId" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_DetectionTypeOccurrences_MediaItemDetectionId",
                table: "DetectionTypeOccurrences",
                column: "MediaItemDetectionId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceAlerts_DeviceId_AlertType",
                table: "DeviceAlerts",
                columns: new[] { "DeviceId", "AlertType" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceCapabilities_DeviceId",
                table: "DeviceCapabilities",
                column: "DeviceId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceConfigSnapshots_DeviceId",
                table: "DeviceConfigSnapshots",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceFeatures_DeviceId",
                table: "DeviceFeatures",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceHealths_DeviceId_CapturedAtUtc",
                table: "DeviceHealths",
                columns: new[] { "DeviceId", "CapturedAtUtc" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthSnapshots_DeviceId_CapturedAtUtc",
                table: "DeviceHealthSnapshots",
                columns: new[] { "DeviceId", "CapturedAtUtc" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthSnapshots_DownloadEventId",
                table: "DeviceHealthSnapshots",
                column: "DownloadEventId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Devices_LocationId",
                table: "Devices",
                column: "LocationId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Devices_LocationId_ProviderDeviceId",
                table: "Devices",
                columns: new[] { "LocationId", "ProviderDeviceId" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_DownloadEvents_DeviceId",
                table: "DownloadEvents",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DownloadEvents_DeviceId_ProviderEventId",
                table: "DownloadEvents",
                columns: new[] { "DeviceId", "ProviderEventId" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_EventDetectedPersons_EventId",
                table: "EventDetectedPersons",
                column: "EventId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_EventDetections_EventId",
                table: "EventDetections",
                column: "EventId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_EventDetectionTypeOccurrences_EventDetectionId",
                table: "EventDetectionTypeOccurrences",
                column: "EventDetectionId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_EventDetectionZones_EventDetectionId_ZoneId",
                table: "EventDetectionZones",
                columns: new[] { "EventDetectionId", "ZoneId" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_Events_DeviceId",
                table: "Events",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Events_DeviceId_ProviderEventId",
                table: "Events",
                columns: new[] { "DeviceId", "ProviderEventId" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_Events_EventDetectionId",
                table: "Events",
                column: "EventDetectionId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_EventSecurityAlerts_EventId",
                table: "EventSecurityAlerts",
                column: "EventId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ExportAuditRecords_ExportedAtUtc",
                table: "ExportAuditRecords",
                column: "ExportedAtUtc");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ExportAuditRecords_LocationId",
                table: "ExportAuditRecords",
                column: "LocationId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ExportRecordItems_ExportRecordId",
                table: "ExportRecordItems",
                column: "ExportRecordId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ExportRecordItems_MediaItemId",
                table: "ExportRecordItems",
                column: "MediaItemId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ExportRecords_ExportedAtUtc",
                table: "ExportRecords",
                column: "ExportedAtUtc");

            _ = migrationBuilder.CreateIndex(
                name: "IX_IntegrityRecords_MediaItemId",
                table: "IntegrityRecords",
                column: "MediaItemId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_JammingIncidentRecords_DeviceId",
                table: "JammingIncidentRecords",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_JammingStatsSummaries_DeviceId",
                table: "JammingStatsSummaries",
                column: "DeviceId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_MediaItemId",
                table: "LegalHolds",
                column: "MediaItemId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_LocationMetadata_LocationId",
                table: "LocationMetadata",
                column: "LocationId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_Locations_ProviderLocationId",
                table: "Locations",
                column: "ProviderLocationId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_MediaItemDetections_MediaItemId",
                table: "MediaItemDetections",
                column: "MediaItemId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_MediaItems_DeviceId",
                table: "MediaItems",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_MediaItems_DeviceId_RecordedAtUtc",
                table: "MediaItems",
                columns: new[] { "DeviceId", "RecordedAtUtc" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_MediaItems_DownloadEventId",
                table: "MediaItems",
                column: "DownloadEventId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_MediaItems_Sha256Hash",
                table: "MediaItems",
                column: "Sha256Hash");

            _ = migrationBuilder.CreateIndex(
                name: "IX_OperatorPreferences_OperatorId",
                table: "OperatorPreferences",
                column: "OperatorId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_PairedDevices_FallbackApiKeyHash",
                table: "PairedDevices",
                column: "FallbackApiKeyHash");

            _ = migrationBuilder.CreateIndex(
                name: "IX_PairedDevices_OperatorId",
                table: "PairedDevices",
                column: "OperatorId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_PairedDevices_WebAuthnCredentialId",
                table: "PairedDevices",
                column: "WebAuthnCredentialId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProviderAccounts_UserId",
                table: "ProviderAccounts",
                column: "UserId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProviderAccounts_UserId_ProviderName",
                table: "ProviderAccounts",
                columns: new[] { "UserId", "ProviderName" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProviderApiCallRecords_ProviderName_TimestampUtc",
                table: "ProviderApiCallRecords",
                columns: new[] { "ProviderName", "TimestampUtc" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProviderApiErrorLogs_DeviceId_OccurredAtUtc",
                table: "ProviderApiErrorLogs",
                columns: new[] { "DeviceId", "OccurredAtUtc" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProviderApiErrorLogs_EventId",
                table: "ProviderApiErrorLogs",
                column: "EventId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProviderReconciliationRecords_DeviceId",
                table: "ProviderReconciliationRecords",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_RingAccountFeatures_RingAccountId",
                table: "RingAccountFeatures",
                column: "RingAccountId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_RingAccounts_ProviderAccountId",
                table: "RingAccounts",
                column: "ProviderAccountId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_RingAccounts_RingAccountFeaturesId",
                table: "RingAccounts",
                column: "RingAccountFeaturesId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditLogEntries_OperatorId",
                table: "SecurityAuditLogEntries",
                column: "OperatorId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditLogEntries_TimestampUtc",
                table: "SecurityAuditLogEntries",
                column: "TimestampUtc");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Users_ProviderUserKey",
                table: "Users",
                column: "ProviderUserKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "AccessAuditLogs");

            _ = migrationBuilder.DropTable(
                name: "ActionLogEntries");

            _ = migrationBuilder.DropTable(
                name: "AiAnalysisMotionZones");

            _ = migrationBuilder.DropTable(
                name: "AiAnalysisTags");

            _ = migrationBuilder.DropTable(
                name: "AppSettings");

            _ = migrationBuilder.DropTable(
                name: "Credentials");

            _ = migrationBuilder.DropTable(
                name: "DetectedPersons");

            _ = migrationBuilder.DropTable(
                name: "DetectionTypeOccurrences");

            _ = migrationBuilder.DropTable(
                name: "DeviceAlerts");

            _ = migrationBuilder.DropTable(
                name: "DeviceCapabilities");

            _ = migrationBuilder.DropTable(
                name: "DeviceConfigSnapshots");

            _ = migrationBuilder.DropTable(
                name: "DeviceFeatures");

            _ = migrationBuilder.DropTable(
                name: "DeviceHealths");

            _ = migrationBuilder.DropTable(
                name: "DeviceHealthSnapshots");

            _ = migrationBuilder.DropTable(
                name: "Devices");

            _ = migrationBuilder.DropTable(
                name: "DownloadEvents");

            _ = migrationBuilder.DropTable(
                name: "EventDetectedPersons");

            _ = migrationBuilder.DropTable(
                name: "EventDetections");

            _ = migrationBuilder.DropTable(
                name: "EventDetectionTypeOccurrences");

            _ = migrationBuilder.DropTable(
                name: "EventDetectionZones");

            _ = migrationBuilder.DropTable(
                name: "Events");

            _ = migrationBuilder.DropTable(
                name: "EventSecurityAlerts");

            _ = migrationBuilder.DropTable(
                name: "ExportAuditRecords");

            _ = migrationBuilder.DropTable(
                name: "ExportRecordItems");

            _ = migrationBuilder.DropTable(
                name: "ExportRecords");

            _ = migrationBuilder.DropTable(
                name: "IntegrityRecords");

            _ = migrationBuilder.DropTable(
                name: "JammingIncidentRecords");

            _ = migrationBuilder.DropTable(
                name: "JammingStatsSummaries");

            _ = migrationBuilder.DropTable(
                name: "LegalHolds");

            _ = migrationBuilder.DropTable(
                name: "LocationMetadata");

            _ = migrationBuilder.DropTable(
                name: "Locations");

            _ = migrationBuilder.DropTable(
                name: "MediaItemDetections");

            _ = migrationBuilder.DropTable(
                name: "MediaItems");

            _ = migrationBuilder.DropTable(
                name: "OperatorPreferences");

            _ = migrationBuilder.DropTable(
                name: "Operators");

            _ = migrationBuilder.DropTable(
                name: "PairedDevices");

            _ = migrationBuilder.DropTable(
                name: "ProviderAccounts");

            _ = migrationBuilder.DropTable(
                name: "ProviderApiCallRecords");

            _ = migrationBuilder.DropTable(
                name: "ProviderApiErrorLogs");

            _ = migrationBuilder.DropTable(
                name: "ProviderReconciliationRecords");

            _ = migrationBuilder.DropTable(
                name: "RingAccountFeatures");

            _ = migrationBuilder.DropTable(
                name: "RingAccounts");

            _ = migrationBuilder.DropTable(
                name: "SecurityAuditLogEntries");

            _ = migrationBuilder.DropTable(
                name: "Users");

            _ = migrationBuilder.DropTable(
                name: "AiAnalysisSnapshots");
        }
    }
}
