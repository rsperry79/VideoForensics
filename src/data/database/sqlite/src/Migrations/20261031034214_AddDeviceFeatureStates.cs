using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceFeatureStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlexaPlusGreetings",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AnimalDetectionEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoShutoffDevice",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoZoomTrack",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutomatedSiren",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CfesEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DynamicNetworkSwitchingEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MotionZoneRecommendation",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PackageWarning",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PropertyView",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Recording24x7Eligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RemoteAccessControl",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RichNotificationsEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SheilaCameraEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SheilaCameraProcessingEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShortOnLongOff",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Show24x7Lite",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOfflineMotionEvents",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Tracklight",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VehicleDetectionEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceFeatureStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FeatureKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Eligible = table.Column<bool>(type: "INTEGER", nullable: true),
                    Allowed = table.Column<bool>(type: "INTEGER", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsEligible = table.Column<bool>(type: "INTEGER", nullable: true),
                    AllowsNoLocation = table.Column<bool>(type: "INTEGER", nullable: true),
                    MaxDigitalZoomLevel = table.Column<double>(type: "REAL", nullable: true),
                    RtMaxDigitalZoomLevel = table.Column<double>(type: "REAL", nullable: true),
                    Score = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Learning = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    RecordingMode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RecordingEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    RecordingState = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ContinuousSupported = table.Column<bool>(type: "INTEGER", nullable: true),
                    ContinuousAvailable = table.Column<bool>(type: "INTEGER", nullable: true),
                    ContinuousUnavailabilityReason = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceFeatureStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceFeatureReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FeatureStateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceFeatureReasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceFeatureReasons_DeviceFeatureStates_FeatureStateId",
                        column: x => x.FeatureStateId,
                        principalTable: "DeviceFeatureStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFeatureReasons_FeatureStateId",
                table: "DeviceFeatureReasons",
                column: "FeatureStateId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFeatureStates_DeviceId_FeatureKey",
                table: "DeviceFeatureStates",
                columns: new[] { "DeviceId", "FeatureKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceFeatureReasons");

            migrationBuilder.DropTable(
                name: "DeviceFeatureStates");

            migrationBuilder.DropColumn(
                name: "AlexaPlusGreetings",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "AnimalDetectionEligible",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "AutoShutoffDevice",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "AutoZoomTrack",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "AutomatedSiren",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "CfesEligible",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "DynamicNetworkSwitchingEligible",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "MotionZoneRecommendation",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "PackageWarning",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "PropertyView",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "Recording24x7Eligible",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "RemoteAccessControl",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "RichNotificationsEligible",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "SheilaCameraEligible",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "SheilaCameraProcessingEligible",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "ShortOnLongOff",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "Show24x7Lite",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "ShowOfflineMotionEvents",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "Tracklight",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "VehicleDetectionEligible",
                table: "DeviceFeatures");
        }
    }
}
