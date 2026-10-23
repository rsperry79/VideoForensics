using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileRingtoneGroupModeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LightGroupSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsOwner = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LightGroupSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingAccountProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FirstNameExtra = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastNameExtra = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TfaPhoneNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TfaEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    CfesEnrolled = table.Column<bool>(type: "INTEGER", nullable: true),
                    HardwareId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ExplorerProgramTerms = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UserFlow = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AppBrand = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AccountType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TsvState = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccountProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingAccountRingtoneDefaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DefaultDingId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DefaultDingUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DefaultMotionId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DefaultMotionUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DefaultAlarmUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DefaultChirpUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DefaultAppstoreUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccountRingtoneDefaults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingAccountRingtones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingtoneId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Available = table.Column<bool>(type: "INTEGER", nullable: true),
                    Checksum = table.Column<int>(type: "INTEGER", nullable: true),
                    SampleRateKhz = table.Column<int>(type: "INTEGER", nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    UrlAmz = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccountRingtones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationModes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    LastUpdateTimeMs = table.Column<long>(type: "INTEGER", nullable: true),
                    SecurityStatusMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SecurityStatusLastUpdateMs = table.Column<long>(type: "INTEGER", nullable: true),
                    ReadOnly = table.Column<bool>(type: "INTEGER", nullable: true),
                    ModePanelType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationModes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationModeSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LastUpdateTimeMs = table.Column<long>(type: "INTEGER", nullable: true),
                    DefaultCameraOnlyMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationModeSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationModeSharings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SharedUsersEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastUpdateTimeMs = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationModeSharings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LightGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LightGroupSetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderDeviceGroupId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LightGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LightGroups_LightGroupSets_LightGroupSetId",
                        column: x => x.LightGroupSetId,
                        principalTable: "LightGroupSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingAccountProfileFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ValueKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BoolValue = table.Column<bool>(type: "INTEGER", nullable: true),
                    IntValue = table.Column<long>(type: "INTEGER", nullable: true),
                    StringValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccountProfileFlags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingAccountProfileFlags_RingAccountProfiles_RingAccountProfileId",
                        column: x => x.RingAccountProfileId,
                        principalTable: "RingAccountProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingAccountSessionFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RemoteLoggingFormatStoring = table.Column<bool>(type: "INTEGER", nullable: true),
                    RemoteLoggingLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    SubscriptionsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    StickupCamSetupEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    VodEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    NwEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    NwUserActivated = table.Column<bool>(type: "INTEGER", nullable: true),
                    RingPlusEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    LpdEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ReactiveSnoozingEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ProActiveSnoozingEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    OwnerProActiveSnoozingEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    LiveviewSettingsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    DeleteAllSettingsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    PowerCableEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    DeviceHealthAlertsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ChimeProEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    MultipleCallsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    UjetEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    MultipleDeleteEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    DeleteAllEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    LdpMotionAnnouncementEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    StarredEventsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ChimeDndEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    VideoSearchEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    FloodlightCamEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    NwLargerAreaEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    RingCamBatteryEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    EliteCamEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    DoorbellV2Enabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    SpotlightBatteryDashboardControlsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    BypassAccountVerification = table.Column<bool>(type: "INTEGER", nullable: true),
                    LegacyCvrRetentionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    NewDashboardEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    RingCamEnabled = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccountSessionFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingAccountSessionFeatures_RingAccountProfiles_RingAccountProfileId",
                        column: x => x.RingAccountProfileId,
                        principalTable: "RingAccountProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingAccountRingtoneDeviceKinds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountRingtoneId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceKind = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccountRingtoneDeviceKinds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingAccountRingtoneDeviceKinds_RingAccountRingtones_RingAccountRingtoneId",
                        column: x => x.RingAccountRingtoneId,
                        principalTable: "RingAccountRingtones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationModeNotYetParticipating",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingLocationModeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ValueJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationModeNotYetParticipating", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationModeNotYetParticipating_RingLocationModes_RingLocationModeId",
                        column: x => x.RingLocationModeId,
                        principalTable: "RingLocationModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationModeSettingsEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingLocationModeSettingsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    DeviceIdType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationModeSettingsEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationModeSettingsEntries_RingLocationModeSettings_RingLocationModeSettingsId",
                        column: x => x.RingLocationModeSettingsId,
                        principalTable: "RingLocationModeSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationModeSupportedActionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingLocationModeSettingsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationModeSupportedActionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationModeSupportedActionGroups_RingLocationModeSettings_RingLocationModeSettingsId",
                        column: x => x.RingLocationModeSettingsId,
                        principalTable: "RingLocationModeSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LightGroupDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LightGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderDeviceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LightGroupDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LightGroupDevices_LightGroups_LightGroupId",
                        column: x => x.LightGroupId,
                        principalTable: "LightGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingAccountProfileFlagItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountProfileFlagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccountProfileFlagItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingAccountProfileFlagItems_RingAccountProfileFlags_RingAccountProfileFlagId",
                        column: x => x.RingAccountProfileFlagId,
                        principalTable: "RingAccountProfileFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationModeSettingsEntryActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingLocationModeSettingsEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ActionGroup = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationModeSettingsEntryActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationModeSettingsEntryActions_RingLocationModeSettingsEntries_RingLocationModeSettingsEntryId",
                        column: x => x.RingLocationModeSettingsEntryId,
                        principalTable: "RingLocationModeSettingsEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LightGroupDevices_LightGroupId",
                table: "LightGroupDevices",
                column: "LightGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_LightGroups_LightGroupSetId",
                table: "LightGroups",
                column: "LightGroupSetId");

            migrationBuilder.CreateIndex(
                name: "IX_LightGroupSets_ProviderLocationId",
                table: "LightGroupSets",
                column: "ProviderLocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingAccountProfileFlagItems_RingAccountProfileFlagId",
                table: "RingAccountProfileFlagItems",
                column: "RingAccountProfileFlagId");

            migrationBuilder.CreateIndex(
                name: "IX_RingAccountProfileFlags_RingAccountProfileId",
                table: "RingAccountProfileFlags",
                column: "RingAccountProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RingAccountProfiles_RingAccountId",
                table: "RingAccountProfiles",
                column: "RingAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingAccountRingtoneDefaults_RingAccountId",
                table: "RingAccountRingtoneDefaults",
                column: "RingAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingAccountRingtoneDeviceKinds_RingAccountRingtoneId",
                table: "RingAccountRingtoneDeviceKinds",
                column: "RingAccountRingtoneId");

            migrationBuilder.CreateIndex(
                name: "IX_RingAccountRingtones_RingAccountId_RingtoneId",
                table: "RingAccountRingtones",
                columns: new[] { "RingAccountId", "RingtoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingAccountSessionFeatures_RingAccountProfileId",
                table: "RingAccountSessionFeatures",
                column: "RingAccountProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationModeNotYetParticipating_RingLocationModeId",
                table: "RingLocationModeNotYetParticipating",
                column: "RingLocationModeId");

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationModes_ProviderLocationId",
                table: "RingLocationModes",
                column: "ProviderLocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationModeSettings_ProviderLocationId",
                table: "RingLocationModeSettings",
                column: "ProviderLocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationModeSettingsEntries_RingLocationModeSettingsId",
                table: "RingLocationModeSettingsEntries",
                column: "RingLocationModeSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationModeSettingsEntryActions_RingLocationModeSettingsEntryId",
                table: "RingLocationModeSettingsEntryActions",
                column: "RingLocationModeSettingsEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationModeSharings_ProviderLocationId",
                table: "RingLocationModeSharings",
                column: "ProviderLocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationModeSupportedActionGroups_RingLocationModeSettingsId",
                table: "RingLocationModeSupportedActionGroups",
                column: "RingLocationModeSettingsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LightGroupDevices");

            migrationBuilder.DropTable(
                name: "RingAccountProfileFlagItems");

            migrationBuilder.DropTable(
                name: "RingAccountRingtoneDefaults");

            migrationBuilder.DropTable(
                name: "RingAccountRingtoneDeviceKinds");

            migrationBuilder.DropTable(
                name: "RingAccountSessionFeatures");

            migrationBuilder.DropTable(
                name: "RingLocationModeNotYetParticipating");

            migrationBuilder.DropTable(
                name: "RingLocationModeSettingsEntryActions");

            migrationBuilder.DropTable(
                name: "RingLocationModeSharings");

            migrationBuilder.DropTable(
                name: "RingLocationModeSupportedActionGroups");

            migrationBuilder.DropTable(
                name: "LightGroups");

            migrationBuilder.DropTable(
                name: "RingAccountProfileFlags");

            migrationBuilder.DropTable(
                name: "RingAccountRingtones");

            migrationBuilder.DropTable(
                name: "RingLocationModes");

            migrationBuilder.DropTable(
                name: "RingLocationModeSettingsEntries");

            migrationBuilder.DropTable(
                name: "LightGroupSets");

            migrationBuilder.DropTable(
                name: "RingAccountProfiles");

            migrationBuilder.DropTable(
                name: "RingLocationModeSettings");
        }
    }
}
