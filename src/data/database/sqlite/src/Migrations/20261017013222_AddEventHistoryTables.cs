using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddEventHistoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RingAppLocationHistoryMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Seq = table.Column<int>(type: "INTEGER", nullable: false),
                    Msg = table.Column<string>(type: "TEXT", nullable: true),
                    DataType = table.Column<string>(type: "TEXT", nullable: true),
                    Src = table.Column<string>(type: "TEXT", nullable: true),
                    EventId = table.Column<string>(type: "TEXT", nullable: true),
                    EventPublishedTsMs = table.Column<long>(type: "INTEGER", nullable: true),
                    RequestSeq = table.Column<long>(type: "INTEGER", nullable: true),
                    AffectedEntityId = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceFamily = table.Column<string>(type: "TEXT", nullable: true),
                    InitiatingEntityType = table.Column<string>(type: "TEXT", nullable: true),
                    AffectedEntityType = table.Column<string>(type: "TEXT", nullable: true),
                    ForwarderTopic = table.Column<string>(type: "TEXT", nullable: true),
                    AccountId = table.Column<string>(type: "TEXT", nullable: true),
                    RequestSessionId = table.Column<long>(type: "INTEGER", nullable: true),
                    EventLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    InitiatingEntityId = table.Column<string>(type: "TEXT", nullable: true),
                    EventOccurredTsMs = table.Column<long>(type: "INTEGER", nullable: true),
                    AffectedEntityName = table.Column<string>(type: "TEXT", nullable: true),
                    ProgramId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAppLocationHistoryMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingDoorbotHistoryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderEventId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: true),
                    Answered = table.Column<bool>(type: "INTEGER", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", nullable: true),
                    Favorite = table.Column<bool>(type: "INTEGER", nullable: true),
                    SnapshotUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Duration = table.Column<int>(type: "INTEGER", nullable: true),
                    IsE2ee = table.Column<bool>(type: "INTEGER", nullable: true),
                    HadSubscription = table.Column<bool>(type: "INTEGER", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: true),
                    Riid = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDoorbotHistoryEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingEventCvProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonDetected = table.Column<bool>(type: "INTEGER", nullable: true),
                    StreamBroken = table.Column<bool>(type: "INTEGER", nullable: true),
                    DetectionType = table.Column<string>(type: "TEXT", nullable: true),
                    FullDescription = table.Column<string>(type: "TEXT", nullable: true),
                    ShortDescription = table.Column<string>(type: "TEXT", nullable: true),
                    Similarity = table.Column<double>(type: "REAL", nullable: true),
                    Anomaly = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventCvProperties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingEventDevicePlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Room = table.Column<string>(type: "TEXT", nullable: true),
                    Area = table.Column<string>(type: "TEXT", nullable: true),
                    ZoneId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventDevicePlacements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingEventDoorbots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DoorbotId = table.Column<long>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventDoorbots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingEventGeolocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    Accuracy = table.Column<double>(type: "REAL", nullable: true),
                    TimestampMs = table.Column<long>(type: "INTEGER", nullable: true),
                    LocationSource = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventGeolocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingEventLastLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    Accuracy = table.Column<double>(type: "REAL", nullable: true),
                    TimestampMs = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventLastLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingEventProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsAlexa = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsSidewalk = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsAutoreply = table.Column<bool>(type: "INTEGER", nullable: true),
                    StarkReviewed = table.Column<bool>(type: "INTEGER", nullable: true),
                    PackagePickupDetected = table.Column<bool>(type: "INTEGER", nullable: true),
                    DetectionConfidence = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventProperties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingEventRecordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventRecordings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingEventSirens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Armed = table.Column<bool>(type: "INTEGER", nullable: true),
                    TriggeredAt = table.Column<long>(type: "INTEGER", nullable: true),
                    TriggerType = table.Column<string>(type: "TEXT", nullable: true),
                    AlarmState = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventSirens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingEventSubscriptionReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", nullable: true),
                    SubscriptionType = table.Column<string>(type: "TEXT", nullable: true),
                    CapturedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventSubscriptionReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationEventPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", nullable: false),
                    PaginationKey = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationEventPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderEventId = table.Column<string>(type: "TEXT", nullable: true),
                    SourceId = table.Column<string>(type: "TEXT", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    Favorite = table.Column<bool>(type: "INTEGER", nullable: true),
                    Recorded = table.Column<bool>(type: "INTEGER", nullable: true),
                    RecordingStatus = table.Column<string>(type: "TEXT", nullable: true),
                    IsE2ee = table.Column<bool>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: true),
                    HadSubscription = table.Column<bool>(type: "INTEGER", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: true),
                    Riid = table.Column<string>(type: "TEXT", nullable: true),
                    DoorbotId = table.Column<long>(type: "INTEGER", nullable: true),
                    DingId = table.Column<long>(type: "INTEGER", nullable: true),
                    DingIdString = table.Column<string>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", nullable: true),
                    BatteryPercentage = table.Column<string>(type: "TEXT", nullable: true),
                    BatteryStatus = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationHistoryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderEventId = table.Column<string>(type: "TEXT", nullable: true),
                    Schema = table.Column<string>(type: "TEXT", nullable: true),
                    SourceId = table.Column<string>(type: "TEXT", nullable: true),
                    StartTime = table.Column<string>(type: "TEXT", nullable: true),
                    EndTime = table.Column<string>(type: "TEXT", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", nullable: true),
                    SourceType = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    SessionDurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    HadSubscription = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: true),
                    RecordingStatus = table.Column<string>(type: "TEXT", nullable: true),
                    Origin = table.Column<string>(type: "TEXT", nullable: true),
                    OriginDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: true),
                    DeleteInProgress = table.Column<bool>(type: "INTEGER", nullable: true),
                    LowConfidence = table.Column<bool>(type: "INTEGER", nullable: true),
                    Similarity = table.Column<double>(type: "REAL", nullable: true),
                    Riid = table.Column<string>(type: "TEXT", nullable: true),
                    UserSpecifiedRecordingDisabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsWatched = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsPropertyViewEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: true),
                    UserActionDisabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    Mission = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationHistoryEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationHistoryPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", nullable: false),
                    PaginationKey = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationHistoryPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingVideoSearchEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderEventId = table.Column<string>(type: "TEXT", nullable: true),
                    RingItemId = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAtMs = table.Column<long>(type: "INTEGER", nullable: true),
                    UpdatedAtMs = table.Column<long>(type: "INTEGER", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    Favorite = table.Column<bool>(type: "INTEGER", nullable: true),
                    HqUrl = table.Column<string>(type: "TEXT", nullable: true),
                    LqUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", nullable: true),
                    UntranscodedUrl = table.Column<string>(type: "TEXT", nullable: true),
                    IsE2ee = table.Column<bool>(type: "INTEGER", nullable: true),
                    ManifestId = table.Column<string>(type: "TEXT", nullable: true),
                    PrerollDuration = table.Column<double>(type: "REAL", nullable: true),
                    HadSubscription = table.Column<bool>(type: "INTEGER", nullable: true),
                    RadarDataUrl = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: true),
                    SourceId = table.Column<string>(type: "TEXT", nullable: true),
                    Riid = table.Column<string>(type: "TEXT", nullable: true),
                    BatteryPercentage = table.Column<string>(type: "TEXT", nullable: true),
                    BatteryStatus = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingVideoSearchEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingAppLocationHistoryBodies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAppLocationHistoryMessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceType = table.Column<string>(type: "TEXT", nullable: true),
                    CatalogId = table.Column<string>(type: "TEXT", nullable: true),
                    Zid = table.Column<string>(type: "TEXT", nullable: true),
                    LastUpdate = table.Column<long>(type: "INTEGER", nullable: true),
                    AdapterType = table.Column<string>(type: "TEXT", nullable: true),
                    BatteryStatus = table.Column<string>(type: "TEXT", nullable: true),
                    SubCategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    RoomName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAppLocationHistoryBodies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingAppLocationHistoryBodies_RingAppLocationHistoryMessages_RingAppLocationHistoryMessageId",
                        column: x => x.RingAppLocationHistoryMessageId,
                        principalTable: "RingAppLocationHistoryMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingEventCvDetectionDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingEventCvPropertiesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    TimestampMs = table.Column<long>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: true),
                    ModelVersion = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventCvDetectionDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingEventCvDetectionDetails_RingEventCvProperties_RingEventCvPropertiesId",
                        column: x => x.RingEventCvPropertiesId,
                        principalTable: "RingEventCvProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingEventCvDetectionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingEventCvPropertiesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectionType = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventCvDetectionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingEventCvDetectionTypes_RingEventCvProperties_RingEventCvPropertiesId",
                        column: x => x.RingEventCvPropertiesId,
                        principalTable: "RingEventCvProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingEventCvProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingEventCvPropertiesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventCvProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingEventCvProfiles_RingEventCvProperties_RingEventCvPropertiesId",
                        column: x => x.RingEventCvPropertiesId,
                        principalTable: "RingEventCvProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingEventCvSecurityAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingEventCvPropertiesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventCvSecurityAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingEventCvSecurityAlerts_RingEventCvProperties_RingEventCvPropertiesId",
                        column: x => x.RingEventCvPropertiesId,
                        principalTable: "RingEventCvProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingEventCvTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingEventCvPropertiesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Tag = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventCvTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingEventCvTags_RingEventCvProperties_RingEventCvPropertiesId",
                        column: x => x.RingEventCvPropertiesId,
                        principalTable: "RingEventCvProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationHistoryVisualizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingLocationHistoryEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Schema = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationHistoryVisualizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationHistoryVisualizations_RingLocationHistoryEvents_RingLocationHistoryEventId",
                        column: x => x.RingLocationHistoryEventId,
                        principalTable: "RingLocationHistoryEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingAppLocationHistoryDeviceStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAppLocationHistoryBodyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: true),
                    SirenState = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAppLocationHistoryDeviceStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingAppLocationHistoryDeviceStates_RingAppLocationHistoryBodies_RingAppLocationHistoryBodyId",
                        column: x => x.RingAppLocationHistoryBodyId,
                        principalTable: "RingAppLocationHistoryBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingAppLocationHistoryImpulses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAppLocationHistoryBodyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ImpulseType = table.Column<string>(type: "TEXT", nullable: true),
                    CommandType = table.Column<string>(type: "TEXT", nullable: true),
                    DataMode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAppLocationHistoryImpulses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingAppLocationHistoryImpulses_RingAppLocationHistoryBodies_RingAppLocationHistoryBodyId",
                        column: x => x.RingAppLocationHistoryBodyId,
                        principalTable: "RingAppLocationHistoryBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingEventCvDetectionZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingEventCvDetectionDetailId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ZoneId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventCvDetectionZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingEventCvDetectionZones_RingEventCvDetectionDetails_RingEventCvDetectionDetailId",
                        column: x => x.RingEventCvDetectionDetailId,
                        principalTable: "RingEventCvDetectionDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingEventCvVerifiedTimestamps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingEventCvDetectionTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    TimestampMs = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventCvVerifiedTimestamps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingEventCvVerifiedTimestamps_RingEventCvDetectionTypes_RingEventCvDetectionTypeId",
                        column: x => x.RingEventCvDetectionTypeId,
                        principalTable: "RingEventCvDetectionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingEventCvSecurityAlertItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingEventCvSecurityAlertId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    AlertText = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingEventCvSecurityAlertItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingEventCvSecurityAlertItems_RingEventCvSecurityAlerts_RingEventCvSecurityAlertId",
                        column: x => x.RingEventCvSecurityAlertId,
                        principalTable: "RingEventCvSecurityAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationHistoryMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingLocationHistoryVisualizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Schema = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    StartTime = table.Column<string>(type: "TEXT", nullable: true),
                    EndTime = table.Column<string>(type: "TEXT", nullable: true),
                    IsE2ee = table.Column<bool>(type: "INTEGER", nullable: true),
                    ManifestId = table.Column<string>(type: "TEXT", nullable: true),
                    FileType = table.Column<string>(type: "TEXT", nullable: true),
                    FileFamily = table.Column<string>(type: "TEXT", nullable: true),
                    PrerollDurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    PlaybackDuration = table.Column<long>(type: "INTEGER", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationHistoryMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationHistoryMedia_RingLocationHistoryVisualizations_RingLocationHistoryVisualizationId",
                        column: x => x.RingLocationHistoryVisualizationId,
                        principalTable: "RingLocationHistoryVisualizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingAppLocationHistoryDeviceModes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAppLocationHistoryDeviceStateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModeName = table.Column<string>(type: "TEXT", nullable: false),
                    TransitionDelay = table.Column<int>(type: "INTEGER", nullable: true),
                    Editable = table.Column<bool>(type: "INTEGER", nullable: true),
                    Removable = table.Column<bool>(type: "INTEGER", nullable: true),
                    ChirpsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    EntryDelay = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAppLocationHistoryDeviceModes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingAppLocationHistoryDeviceModes_RingAppLocationHistoryDeviceStates_RingAppLocationHistoryDeviceStateId",
                        column: x => x.RingAppLocationHistoryDeviceStateId,
                        principalTable: "RingAppLocationHistoryDeviceStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationHistoryMediaMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingLocationHistoryMediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Recording24x7Enabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    DingId = table.Column<string>(type: "TEXT", nullable: true),
                    AudioEncoding = table.Column<string>(type: "TEXT", nullable: true),
                    VideoEncoding = table.Column<string>(type: "TEXT", nullable: true),
                    TbaTranscodingEnabled = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationHistoryMediaMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationHistoryMediaMetadata_RingLocationHistoryMedia_RingLocationHistoryMediaId",
                        column: x => x.RingLocationHistoryMediaId,
                        principalTable: "RingLocationHistoryMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationHistoryMediaTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingLocationHistoryMediaMetadataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackId = table.Column<string>(type: "TEXT", nullable: true),
                    Codec = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationHistoryMediaTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationHistoryMediaTracks_RingLocationHistoryMediaMetadata_RingLocationHistoryMediaMetadataId",
                        column: x => x.RingLocationHistoryMediaMetadataId,
                        principalTable: "RingLocationHistoryMediaMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RingAppLocationHistoryBodies_RingAppLocationHistoryMessageId",
                table: "RingAppLocationHistoryBodies",
                column: "RingAppLocationHistoryMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_RingAppLocationHistoryDeviceModes_RingAppLocationHistoryDeviceStateId",
                table: "RingAppLocationHistoryDeviceModes",
                column: "RingAppLocationHistoryDeviceStateId");

            migrationBuilder.CreateIndex(
                name: "IX_RingAppLocationHistoryDeviceStates_RingAppLocationHistoryBodyId",
                table: "RingAppLocationHistoryDeviceStates",
                column: "RingAppLocationHistoryBodyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingAppLocationHistoryImpulses_RingAppLocationHistoryBodyId",
                table: "RingAppLocationHistoryImpulses",
                column: "RingAppLocationHistoryBodyId");

            migrationBuilder.CreateIndex(
                name: "IX_RingAppLocationHistoryMessages_EventId",
                table: "RingAppLocationHistoryMessages",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDoorbotHistoryEvents_ProviderEventId",
                table: "RingDoorbotHistoryEvents",
                column: "ProviderEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingEventCvDetectionDetails_RingEventCvPropertiesId",
                table: "RingEventCvDetectionDetails",
                column: "RingEventCvPropertiesId");

            migrationBuilder.CreateIndex(
                name: "IX_RingEventCvDetectionTypes_RingEventCvPropertiesId",
                table: "RingEventCvDetectionTypes",
                column: "RingEventCvPropertiesId");

            migrationBuilder.CreateIndex(
                name: "IX_RingEventCvDetectionZones_RingEventCvDetectionDetailId",
                table: "RingEventCvDetectionZones",
                column: "RingEventCvDetectionDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_RingEventCvProfiles_RingEventCvPropertiesId",
                table: "RingEventCvProfiles",
                column: "RingEventCvPropertiesId");

            migrationBuilder.CreateIndex(
                name: "IX_RingEventCvProperties_Source_SourceRowId",
                table: "RingEventCvProperties",
                columns: new[] { "Source", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_RingEventCvSecurityAlertItems_RingEventCvSecurityAlertId",
                table: "RingEventCvSecurityAlertItems",
                column: "RingEventCvSecurityAlertId");

            migrationBuilder.CreateIndex(
                name: "IX_RingEventCvSecurityAlerts_RingEventCvPropertiesId",
                table: "RingEventCvSecurityAlerts",
                column: "RingEventCvPropertiesId");

            migrationBuilder.CreateIndex(
                name: "IX_RingEventCvTags_RingEventCvPropertiesId",
                table: "RingEventCvTags",
                column: "RingEventCvPropertiesId");

            migrationBuilder.CreateIndex(
                name: "IX_RingEventCvVerifiedTimestamps_RingEventCvDetectionTypeId",
                table: "RingEventCvVerifiedTimestamps",
                column: "RingEventCvDetectionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RingEventDevicePlacements_Source_SourceRowId",
                table: "RingEventDevicePlacements",
                columns: new[] { "Source", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_RingEventDoorbots_Source_SourceRowId",
                table: "RingEventDoorbots",
                columns: new[] { "Source", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_RingEventGeolocations_Source_SourceRowId",
                table: "RingEventGeolocations",
                columns: new[] { "Source", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_RingEventLastLocations_Source_SourceRowId",
                table: "RingEventLastLocations",
                columns: new[] { "Source", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_RingEventProperties_Source_SourceRowId",
                table: "RingEventProperties",
                columns: new[] { "Source", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_RingEventRecordings_Source_SourceRowId",
                table: "RingEventRecordings",
                columns: new[] { "Source", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_RingEventSirens_Source_SourceRowId",
                table: "RingEventSirens",
                columns: new[] { "Source", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_RingEventSubscriptionReceipts_Source_SourceRowId",
                table: "RingEventSubscriptionReceipts",
                columns: new[] { "Source", "SourceRowId" });

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationEventPages_ProviderLocationId",
                table: "RingLocationEventPages",
                column: "ProviderLocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationEvents_ProviderLocationId_ProviderEventId",
                table: "RingLocationEvents",
                columns: new[] { "ProviderLocationId", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationHistoryEvents_ProviderLocationId_ProviderEventId",
                table: "RingLocationHistoryEvents",
                columns: new[] { "ProviderLocationId", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationHistoryMedia_RingLocationHistoryVisualizationId",
                table: "RingLocationHistoryMedia",
                column: "RingLocationHistoryVisualizationId");

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationHistoryMediaMetadata_RingLocationHistoryMediaId",
                table: "RingLocationHistoryMediaMetadata",
                column: "RingLocationHistoryMediaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationHistoryMediaTracks_RingLocationHistoryMediaMetadataId",
                table: "RingLocationHistoryMediaTracks",
                column: "RingLocationHistoryMediaMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationHistoryPages_ProviderLocationId",
                table: "RingLocationHistoryPages",
                column: "ProviderLocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationHistoryVisualizations_RingLocationHistoryEventId",
                table: "RingLocationHistoryVisualizations",
                column: "RingLocationHistoryEventId");

            migrationBuilder.CreateIndex(
                name: "IX_RingVideoSearchEvents_RingAccountId_ProviderEventId",
                table: "RingVideoSearchEvents",
                columns: new[] { "RingAccountId", "ProviderEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RingAppLocationHistoryDeviceModes");

            migrationBuilder.DropTable(
                name: "RingAppLocationHistoryImpulses");

            migrationBuilder.DropTable(
                name: "RingDoorbotHistoryEvents");

            migrationBuilder.DropTable(
                name: "RingEventCvDetectionZones");

            migrationBuilder.DropTable(
                name: "RingEventCvProfiles");

            migrationBuilder.DropTable(
                name: "RingEventCvSecurityAlertItems");

            migrationBuilder.DropTable(
                name: "RingEventCvTags");

            migrationBuilder.DropTable(
                name: "RingEventCvVerifiedTimestamps");

            migrationBuilder.DropTable(
                name: "RingEventDevicePlacements");

            migrationBuilder.DropTable(
                name: "RingEventDoorbots");

            migrationBuilder.DropTable(
                name: "RingEventGeolocations");

            migrationBuilder.DropTable(
                name: "RingEventLastLocations");

            migrationBuilder.DropTable(
                name: "RingEventProperties");

            migrationBuilder.DropTable(
                name: "RingEventRecordings");

            migrationBuilder.DropTable(
                name: "RingEventSirens");

            migrationBuilder.DropTable(
                name: "RingEventSubscriptionReceipts");

            migrationBuilder.DropTable(
                name: "RingLocationEventPages");

            migrationBuilder.DropTable(
                name: "RingLocationEvents");

            migrationBuilder.DropTable(
                name: "RingLocationHistoryMediaTracks");

            migrationBuilder.DropTable(
                name: "RingLocationHistoryPages");

            migrationBuilder.DropTable(
                name: "RingVideoSearchEvents");

            migrationBuilder.DropTable(
                name: "RingAppLocationHistoryDeviceStates");

            migrationBuilder.DropTable(
                name: "RingEventCvDetectionDetails");

            migrationBuilder.DropTable(
                name: "RingEventCvSecurityAlerts");

            migrationBuilder.DropTable(
                name: "RingEventCvDetectionTypes");

            migrationBuilder.DropTable(
                name: "RingLocationHistoryMediaMetadata");

            migrationBuilder.DropTable(
                name: "RingAppLocationHistoryBodies");

            migrationBuilder.DropTable(
                name: "RingEventCvProperties");

            migrationBuilder.DropTable(
                name: "RingLocationHistoryMedia");

            migrationBuilder.DropTable(
                name: "RingAppLocationHistoryMessages");

            migrationBuilder.DropTable(
                name: "RingLocationHistoryVisualizations");

            migrationBuilder.DropTable(
                name: "RingLocationHistoryEvents");
        }
    }
}
