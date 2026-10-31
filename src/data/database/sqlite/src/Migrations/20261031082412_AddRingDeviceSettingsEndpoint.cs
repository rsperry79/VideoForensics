using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRingDeviceSettingsEndpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RingDeviceAdvancedMotionSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Zone1Name = table.Column<string>(type: "TEXT", nullable: true),
                    Zone1State = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone1Vertex1X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone1Vertex1Y = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex2X = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex2Y = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex3X = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex3Y = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex4X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone1Vertex4Y = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex5X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone1Vertex5Y = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex6X = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex6Y = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex7X = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex7Y = table.Column<double>(type: "REAL", nullable: true),
                    Zone1Vertex8X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone1Vertex8Y = table.Column<double>(type: "REAL", nullable: true),
                    Zone2Name = table.Column<string>(type: "TEXT", nullable: true),
                    Zone2State = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex1X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex1Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex2X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex2Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex3X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex3Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex4X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex4Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex5X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex5Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex6X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex6Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex7X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex7Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex8X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone2Vertex8Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Name = table.Column<string>(type: "TEXT", nullable: true),
                    Zone3State = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex1X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex1Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex2X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex2Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex3X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex3Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex4X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex4Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex5X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex5Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex6X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex6Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex7X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex7Y = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex8X = table.Column<int>(type: "INTEGER", nullable: true),
                    Zone3Vertex8Y = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceAdvancedMotionSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceAdvancedMotionSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceBackendSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnableRichNotifications = table.Column<bool>(type: "INTEGER", nullable: true),
                    LiveViewDisabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    LiveViewPresetProfile = table.Column<string>(type: "TEXT", nullable: true),
                    MotionSnoozePresetProfile = table.Column<string>(type: "TEXT", nullable: true),
                    MotionWarningEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesAiAlexaPlusGreetings = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesAiAutomatedWarnings = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesAiLabsDailyClips = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesAiLabsMemorableMoments = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesAlarmTriggeredRecording = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesAlexaConcierge = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesAnimal = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesAppstore = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesBabyCry = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesCarAlarm = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesClosedCaption = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesCo2SmokeAlarm = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesCriticalAlerts = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesCvTriggers = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesDailyDigest = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesDingCall = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesDogBark = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesExtendedLiveView = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesGeneralSound = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesGlassBreak = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesHuman = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesLiveSpeak = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesLoitering = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesMotion = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesMotionAwareness = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesMotionStop = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesMulticamLiveView = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesNaturalLanguageSearch = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesOtherMotion = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesPackageDelivery = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesPackagePickup = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesPackageProtection = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesPersonIdentification = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesRecording24x7 = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesRetinalTuning = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesRogersVqa = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesSheilaCv = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesSheilaRecording = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesSmartMediaDescriptions = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesSnapshotCapturePlus = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesSystemLevelPip = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesUnverifiedMotion = table.Column<bool>(type: "INTEGER", nullable: true),
                    PaidFeaturesVehicle = table.Column<bool>(type: "INTEGER", nullable: true),
                    TermsOfServiceAcceptedAutoreply = table.Column<bool>(type: "INTEGER", nullable: true),
                    TermsOfServiceAcceptedConcierge = table.Column<bool>(type: "INTEGER", nullable: true),
                    UserSpecifiedFootageHoursTtl = table.Column<int>(type: "INTEGER", nullable: true),
                    UserSpecifiedRecordingTtl = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceBackendSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceBackendSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceClientDeviceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AdvancedMotionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    CfesEligible = table.Column<bool>(type: "INTEGER", nullable: true),
                    DynamicNetworkSwitchingEligible = table.Column<bool>(type: "INTEGER", nullable: true),
                    LightScheduleEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    LocalPlaybackEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    MotionMessageEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    MotionZoneRecommendation = table.Column<bool>(type: "INTEGER", nullable: true),
                    NightVisionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    PeopleOnlyEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    PtzSetupComplete = table.Column<bool>(type: "INTEGER", nullable: true),
                    RichNotificationsEligible = table.Column<bool>(type: "INTEGER", nullable: true),
                    RingtonesEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ShadowCorrectionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    SheilaCameraEligible = table.Column<bool>(type: "INTEGER", nullable: true),
                    SheilaCameraProcessingEligible = table.Column<bool>(type: "INTEGER", nullable: true),
                    Show24x7Lite = table.Column<bool>(type: "INTEGER", nullable: true),
                    ShowOfflineMotionEvents = table.Column<bool>(type: "INTEGER", nullable: true),
                    ShowRadarData = table.Column<bool>(type: "INTEGER", nullable: true),
                    VehicleDetectionEligible = table.Column<bool>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceClientDeviceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceClientDeviceSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceCvSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DetectionTypesVehicleAction = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceCvSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceCvSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceCvThresholdOverrideValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionPosition = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceCvThresholdOverrideValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceCvThresholdOverrideValues_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceCvThresholdOverrideVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceCvThresholdOverrideVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceCvThresholdOverrideVersions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceGeneralSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", nullable: true),
                    CountryCodeEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    DataCollectionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    EnableAudioRecording = table.Column<bool>(type: "INTEGER", nullable: true),
                    EventsViaRss = table.Column<bool>(type: "INTEGER", nullable: true),
                    FirmwaresLocked = table.Column<bool>(type: "INTEGER", nullable: true),
                    Lite24x7Enabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    Lite24x7Subscribed = table.Column<bool>(type: "INTEGER", nullable: true),
                    OfflineMotionEventEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    OfflineMotionEventSubscribed = table.Column<bool>(type: "INTEGER", nullable: true),
                    TheftAlarmEnable = table.Column<bool>(type: "INTEGER", nullable: true),
                    UseWrapupDomain = table.Column<bool>(type: "INTEGER", nullable: true),
                    UtcOffset = table.Column<string>(type: "TEXT", nullable: true),
                    WrapupDomain = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceGeneralSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceGeneralSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceMiscSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttestationRdaEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    AuthFallbackEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    AuthProtocol = table.Column<string>(type: "TEXT", nullable: true),
                    AuthRetryInterval = table.Column<int>(type: "INTEGER", nullable: true),
                    EventOnDemandMaxDurationS = table.Column<int>(type: "INTEGER", nullable: true),
                    FloodlightAlwaysOnDuration = table.Column<string>(type: "TEXT", nullable: true),
                    KeepAliveAuto = table.Column<int>(type: "INTEGER", nullable: true),
                    LightSnoozeDuration = table.Column<int>(type: "INTEGER", nullable: true),
                    Lite24x7Mode = table.Column<string>(type: "TEXT", nullable: true),
                    SheilaSessionRegistrationApi = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: true),
                    VolumeMicVolume = table.Column<int>(type: "INTEGER", nullable: true),
                    VolumeVoiceVolume = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceMiscSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceMiscSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceMotionSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AdvancedMotionDetectionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    AdvancedMotionDetectionHumanOnlyMode = table.Column<bool>(type: "INTEGER", nullable: true),
                    AdvancedMotionDetectionLoiteringMode = table.Column<bool>(type: "INTEGER", nullable: true),
                    AdvancedMotionDetectionMode = table.Column<string>(type: "TEXT", nullable: true),
                    AdvancedMotionGlanceEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    AdvancedMotionRecordingHumanMode = table.Column<bool>(type: "INTEGER", nullable: true),
                    AdvancedMotionZonesEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    AdvancedMotionZonesType = table.Column<string>(type: "TEXT", nullable: true),
                    AdvancedPirMotionZonesZone1Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    AdvancedPirMotionZonesZone2Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    AdvancedPirMotionZonesZone3Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    AdvancedPirMotionZonesZone4Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    AdvancedPirMotionZonesZone5Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    AdvancedPirMotionZonesZone6Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    EnableAudio = table.Column<bool>(type: "INTEGER", nullable: true),
                    EnableIr = table.Column<int>(type: "INTEGER", nullable: true),
                    EnableRecording = table.Column<bool>(type: "INTEGER", nullable: true),
                    EndDetection = table.Column<int>(type: "INTEGER", nullable: true),
                    LoiteringThreshold = table.Column<int>(type: "INTEGER", nullable: true),
                    MotionSensitivityProfile = table.Column<int>(type: "INTEGER", nullable: true),
                    MotionSnoozeDeviceEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    MotionSnoozeThresholdMins = table.Column<int>(type: "INTEGER", nullable: true),
                    ZoneSettingsV2Enabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceMotionSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceMotionSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceMotionSnoozeProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceMotionSnoozeProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceMotionSnoozeProfiles_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceSnapshotSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CloseContainer = table.Column<int>(type: "INTEGER", nullable: true),
                    FrequencyAfterSecs = table.Column<int>(type: "INTEGER", nullable: true),
                    FrequencySecs = table.Column<int>(type: "INTEGER", nullable: true),
                    Lite24x7ResolutionP = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxUploadKb = table.Column<int>(type: "INTEGER", nullable: true),
                    OmeResolutionP = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodAfterSecs = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceSnapshotSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceSnapshotSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceStreamSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActiveStreamingProfile = table.Column<string>(type: "TEXT", nullable: true),
                    Profile = table.Column<int>(type: "INTEGER", nullable: true),
                    StreamingProfilesRmsHost = table.Column<string>(type: "TEXT", nullable: true),
                    StreamingProfilesRmsPort = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceStreamSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceStreamSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceVideoSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AbcEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    AeLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    AeMode = table.Column<int>(type: "INTEGER", nullable: true),
                    AspectRatio = table.Column<string>(type: "TEXT", nullable: true),
                    AutoClipLengthEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    Brightness = table.Column<int>(type: "INTEGER", nullable: true),
                    ClipLengthMax = table.Column<int>(type: "INTEGER", nullable: true),
                    ClipLengthMin = table.Column<int>(type: "INTEGER", nullable: true),
                    Contrast = table.Column<int>(type: "INTEGER", nullable: true),
                    DynamicResolutionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ExposureControl = table.Column<int>(type: "INTEGER", nullable: true),
                    HdrEnable = table.Column<bool>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone1Name = table.Column<string>(type: "TEXT", nullable: true),
                    IgnoreZonesZone1State = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone1Vertex1X = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone1Vertex1Y = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone1Vertex2X = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone1Vertex2Y = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone2Name = table.Column<string>(type: "TEXT", nullable: true),
                    IgnoreZonesZone2State = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone2Vertex1X = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone2Vertex1Y = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone2Vertex2X = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone2Vertex2Y = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone3Name = table.Column<string>(type: "TEXT", nullable: true),
                    IgnoreZonesZone3State = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone3Vertex1X = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone3Vertex1Y = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone3Vertex2X = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone3Vertex2Y = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone4Name = table.Column<string>(type: "TEXT", nullable: true),
                    IgnoreZonesZone4State = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone4Vertex1X = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone4Vertex1Y = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone4Vertex2X = table.Column<int>(type: "INTEGER", nullable: true),
                    IgnoreZonesZone4Vertex2Y = table.Column<int>(type: "INTEGER", nullable: true),
                    NightColorEnable = table.Column<bool>(type: "INTEGER", nullable: true),
                    RetinalTuningEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    RetinalTuningUserSpecifiedEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    Saturation = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceVideoSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceVideoSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceVodSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CachedVodDomain = table.Column<string>(type: "TEXT", nullable: true),
                    Enable = table.Column<bool>(type: "INTEGER", nullable: true),
                    ForcedEnableVod = table.Column<bool>(type: "INTEGER", nullable: true),
                    ToggledAt = table.Column<string>(type: "TEXT", nullable: true),
                    UseCachedVodDomain = table.Column<bool>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceVodSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceVodSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceAdvancedMotionSettings_DeviceId",
                table: "RingDeviceAdvancedMotionSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceBackendSettings_DeviceId",
                table: "RingDeviceBackendSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceClientDeviceSettings_DeviceId",
                table: "RingDeviceClientDeviceSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceCvSettings_DeviceId",
                table: "RingDeviceCvSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceCvThresholdOverrideValues_DeviceId",
                table: "RingDeviceCvThresholdOverrideValues",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceCvThresholdOverrideVersions_DeviceId",
                table: "RingDeviceCvThresholdOverrideVersions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceGeneralSettings_DeviceId",
                table: "RingDeviceGeneralSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceMiscSettings_DeviceId",
                table: "RingDeviceMiscSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceMotionSettings_DeviceId",
                table: "RingDeviceMotionSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceMotionSnoozeProfiles_DeviceId",
                table: "RingDeviceMotionSnoozeProfiles",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceSnapshotSettings_DeviceId",
                table: "RingDeviceSnapshotSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceStreamSettings_DeviceId",
                table: "RingDeviceStreamSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceVideoSettings_DeviceId",
                table: "RingDeviceVideoSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceVodSettings_DeviceId",
                table: "RingDeviceVodSettings",
                column: "DeviceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RingDeviceAdvancedMotionSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceBackendSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceClientDeviceSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceCvSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceCvThresholdOverrideValues");

            migrationBuilder.DropTable(
                name: "RingDeviceCvThresholdOverrideVersions");

            migrationBuilder.DropTable(
                name: "RingDeviceGeneralSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceMiscSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceMotionSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceMotionSnoozeProfiles");

            migrationBuilder.DropTable(
                name: "RingDeviceSnapshotSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceStreamSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceVideoSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceVodSettings");
        }
    }
}
