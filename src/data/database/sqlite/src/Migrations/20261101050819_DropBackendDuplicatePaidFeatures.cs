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
            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesAlarmTriggeredRecording",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesAlexaConcierge",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesAnimal",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesBabyCry",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesCarAlarm",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesClosedCaption",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesCo2SmokeAlarm",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesCriticalAlerts",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesCvTriggers",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesDailyDigest",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesDingCall",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesDogBark",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesExtendedLiveView",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesGeneralSound",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesGlassBreak",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesHuman",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesLiveSpeak",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesLoitering",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesMotion",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesMulticamLiveView",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesNaturalLanguageSearch",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesOtherMotion",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesPackageDelivery",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesPackagePickup",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesPackageProtection",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesRecording24x7",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesSheilaCv",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesSheilaRecording",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesSnapshotCapturePlus",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesSystemLevelPip",
                table: "RingDeviceBackendSettings");

            _ = migrationBuilder.DropColumn(
                name: "PaidFeaturesVehicle",
                table: "RingDeviceBackendSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesAlarmTriggeredRecording",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesAlexaConcierge",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesAnimal",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesBabyCry",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesCarAlarm",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesClosedCaption",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesCo2SmokeAlarm",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesCriticalAlerts",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesCvTriggers",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesDailyDigest",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesDingCall",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesDogBark",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesExtendedLiveView",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesGeneralSound",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesGlassBreak",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesHuman",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesLiveSpeak",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesLoitering",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesMotion",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesMulticamLiveView",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesNaturalLanguageSearch",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesOtherMotion",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesPackageDelivery",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesPackagePickup",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesPackageProtection",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesRecording24x7",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesSheilaCv",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesSheilaRecording",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesSnapshotCapturePlus",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesSystemLevelPip",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PaidFeaturesVehicle",
                table: "RingDeviceBackendSettings",
                type: "INTEGER",
                nullable: true);
        }
    }
}
