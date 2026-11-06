using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DropDuplicateDeviceSettingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "AdvancedMotionDetectionEnabled",
                table: "RingDeviceSettings");

            _ = migrationBuilder.DropColumn(
                name: "AdvancedMotionDetectionHumanOnlyMode",
                table: "RingDeviceSettings");

            _ = migrationBuilder.DropColumn(
                name: "EnableAudioRecording",
                table: "RingDeviceSettings");

            _ = migrationBuilder.DropColumn(
                name: "EnableVod",
                table: "RingDeviceSettings");

            _ = migrationBuilder.DropColumn(
                name: "ExposureControl",
                table: "RingDeviceSettings");

            _ = migrationBuilder.DropColumn(
                name: "LiveViewPresetProfile",
                table: "RingDeviceSettings");

            _ = migrationBuilder.DropColumn(
                name: "MotionSnoozePresetProfile",
                table: "RingDeviceSettings");

            _ = migrationBuilder.DropColumn(
                name: "PirSensitivity1",
                table: "RingDeviceSettings");

            _ = migrationBuilder.DropColumn(
                name: "VodSuspended",
                table: "RingDeviceSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<bool>(
                name: "AdvancedMotionDetectionEnabled",
                table: "RingDeviceSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "AdvancedMotionDetectionHumanOnlyMode",
                table: "RingDeviceSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "EnableAudioRecording",
                table: "RingDeviceSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "EnableVod",
                table: "RingDeviceSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "ExposureControl",
                table: "RingDeviceSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "LiveViewPresetProfile",
                table: "RingDeviceSettings",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "MotionSnoozePresetProfile",
                table: "RingDeviceSettings",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "PirSensitivity1",
                table: "RingDeviceSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "VodSuspended",
                table: "RingDeviceSettings",
                type: "INTEGER",
                nullable: true);
        }
    }
}
