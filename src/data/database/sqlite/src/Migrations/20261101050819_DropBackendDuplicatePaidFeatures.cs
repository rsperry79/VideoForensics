using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DropBackendDuplicatePaidFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidFeaturesAlarmTriggeredRecording",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesAlexaConcierge",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesAnimal",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesBabyCry",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesCarAlarm",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesClosedCaption",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesCo2SmokeAlarm",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesCriticalAlerts",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesCvTriggers",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesDailyDigest",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesDingCall",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesDogBark",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesExtendedLiveView",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesGeneralSound",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesGlassBreak",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesHuman",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesLiveSpeak",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesLoitering",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesMotion",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesMulticamLiveView",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesNaturalLanguageSearch",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesOtherMotion",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesPackageDelivery",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesPackagePickup",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesPackageProtection",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesRecording24x7",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesSheilaCv",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesSheilaRecording",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesSnapshotCapturePlus",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesSystemLevelPip",
                table: "RingDeviceBackendSettings");

            migrationBuilder.DropColumn(
                name: "PaidFeaturesVehicle",
                table: "RingDeviceBackendSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesAlarmTriggeredRecording",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesAlexaConcierge",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesAnimal",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesBabyCry",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesCarAlarm",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesClosedCaption",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesCo2SmokeAlarm",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesCriticalAlerts",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesCvTriggers",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesDailyDigest",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesDingCall",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesDogBark",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesExtendedLiveView",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesGeneralSound",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesGlassBreak",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesHuman",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesLiveSpeak",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesLoitering",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesMotion",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesMulticamLiveView",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesNaturalLanguageSearch",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesOtherMotion",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesPackageDelivery",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesPackagePickup",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesPackageProtection",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesRecording24x7",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesSheilaCv",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesSheilaRecording",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesSnapshotCapturePlus",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesSystemLevelPip",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesVehicle",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);
        }
    }
}
