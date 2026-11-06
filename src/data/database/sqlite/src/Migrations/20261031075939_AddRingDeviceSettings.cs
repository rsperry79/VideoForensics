using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRingDeviceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceGeneralSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VoiceVolume = table.Column<int>(type: "INTEGER", nullable: true),
                    LiveViewDisabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    MotionAnnouncement = table.Column<bool>(type: "INTEGER", nullable: true),
                    LoiteringThreshold = table.Column<int>(type: "INTEGER", nullable: true),
                    UserSpecifiedRecordingTtl = table.Column<int>(type: "INTEGER", nullable: true),
                    ExtendedLiveView = table.Column<int>(type: "INTEGER", nullable: true),
                    StatusLedEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    FloodlightBrightness = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceGeneralSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceGeneralSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StarkEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    StarkEnrolled = table.Column<bool>(type: "INTEGER", nullable: true),
                    EnableRichNotifications = table.Column<bool>(type: "INTEGER", nullable: true),
                    RichNotificationsBillingEligible = table.Column<bool>(type: "INTEGER", nullable: true),
                    RichNotificationsFaceCropEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    RichNotificationsSceneSource = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PeopleDetectionEligible = table.Column<bool>(type: "INTEGER", nullable: true),
                    TermsOfServiceAccepted = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    MotionSensitivityProfile = table.Column<int>(type: "INTEGER", nullable: true),
                    Lite24x7Enabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    Lite24x7FrequencySecs = table.Column<int>(type: "INTEGER", nullable: true),
                    Lite24x7ResolutionP = table.Column<int>(type: "INTEGER", nullable: true),
                    Lite24x7Subscribed = table.Column<bool>(type: "INTEGER", nullable: true),
                    Lite24x7FootageTtl = table.Column<int>(type: "INTEGER", nullable: true),
                    RingMediaServerEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    RingMediaServerHost = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SheilaCvProcessingEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    SheilaLocalStorageEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    EncryptionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    EncryptionMethod = table.Column<int>(type: "INTEGER", nullable: true),
                    EncryptionEligibilityMode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Recording24x7Mode = table.Column<int>(type: "INTEGER", nullable: true),
                    NaturalLanguageSearchEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    NaturalLanguageSearchMode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ConciergeMode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ConciergeAlexaDelayMs = table.Column<int>(type: "INTEGER", nullable: true),
                    ConciergeAutoreplyDelayMs = table.Column<int>(type: "INTEGER", nullable: true),
                    PirZone1Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    PirZone2Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    PirZone3Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    PirZone4Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    PirZone5Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    PirZone6Sensitivity = table.Column<int>(type: "INTEGER", nullable: true),
                    EnableLiveViewAudioOverride = table.Column<bool>(type: "INTEGER", nullable: true),
                    ActiveStreamingEventLedEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    FlickElimRecommendedMode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EnableVod = table.Column<bool>(type: "INTEGER", nullable: true),
                    ExposureControl = table.Column<int>(type: "INTEGER", nullable: true),
                    VodSuspended = table.Column<bool>(type: "INTEGER", nullable: true),
                    PirSensitivity1 = table.Column<int>(type: "INTEGER", nullable: true),
                    EnableAudioRecording = table.Column<bool>(type: "INTEGER", nullable: true),
                    AdvancedMotionDetectionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    AdvancedMotionDetectionHumanOnlyMode = table.Column<bool>(type: "INTEGER", nullable: true),
                    LiveViewPresetProfile = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    MotionSnoozePresetProfile = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RingCreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceGeneralSettings_DeviceId",
                table: "DeviceGeneralSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceSettings_DeviceId",
                table: "RingDeviceSettings",
                column: "DeviceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceGeneralSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceSettings");
        }
    }
}
