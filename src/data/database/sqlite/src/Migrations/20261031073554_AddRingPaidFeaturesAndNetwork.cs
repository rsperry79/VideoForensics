using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRingPaidFeaturesAndNetwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceNetworkSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MacAddressBle = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    MacAddressWifi24 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    MacAddressWifi5 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    MaxDynamicListenInterval = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MultiNetPref = table.Column<int>(type: "INTEGER", nullable: true),
                    DataRestrictionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceNetworkSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceNetworkSettings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceNetworkDiagnosis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChannelAnalysis = table.Column<bool>(type: "INTEGER", nullable: true),
                    PeriodicDiagnosis = table.Column<bool>(type: "INTEGER", nullable: true),
                    LanPerformance = table.Column<bool>(type: "INTEGER", nullable: true),
                    TcpIperf = table.Column<bool>(type: "INTEGER", nullable: true),
                    UdpIperf = table.Column<bool>(type: "INTEGER", nullable: true),
                    TcpHost = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UdpHost = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TcpPort = table.Column<int>(type: "INTEGER", nullable: true),
                    UdpPort = table.Column<int>(type: "INTEGER", nullable: true),
                    UdpBandwidth = table.Column<long>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceNetworkDiagnosis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceNetworkDiagnosis_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDevicePaidFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CvPaidBabyCry = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidCarAlarm = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidCo2SmokeAlarm = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidDogBark = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidGeneralSound = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidGlassBreak = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidHuman = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidLoitering = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidMotion = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidOtherMotion = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidPackageDelivery = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidPackagePickup = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidCvTriggers = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidVehicle = table.Column<bool>(type: "INTEGER", nullable: true),
                    CvPaidAnimal = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidAlexaConcierge = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidSheilaCv = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidSheilaRecording = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidCriticalAlerts = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidSystemLevelPip = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidExtendedLiveView = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidSnapshotCapturePlus = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidLiveSpeak = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidClosedCaption = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidDailyDigest = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidDingCall = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidNaturalLanguageSearch = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidMulticamLiveView = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidPackageProtection = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidAlarmTriggeredRecording = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtherPaidRecording24x7 = table.Column<bool>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDevicePaidFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDevicePaidFeatures_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceNetworkSettings_DeviceId",
                table: "DeviceNetworkSettings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceNetworkDiagnosis_DeviceId",
                table: "RingDeviceNetworkDiagnosis",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDevicePaidFeatures_DeviceId",
                table: "RingDevicePaidFeatures",
                column: "DeviceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceNetworkSettings");

            migrationBuilder.DropTable(
                name: "RingDeviceNetworkDiagnosis");

            migrationBuilder.DropTable(
                name: "RingDevicePaidFeatures");
        }
    }
}
