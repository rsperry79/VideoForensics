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
            _ = migrationBuilder.AddColumn<bool>(
                name: "AlexaPlusGreetings",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "AnimalDetectionEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "AutoShutoffDevice",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "AutoZoomTrack",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "AutomatedSiren",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "CfesEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "DynamicNetworkSwitchingEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "MotionZoneRecommendation",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PackageWarning",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PropertyView",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "Recording24x7Eligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "RemoteAccessControl",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "RichNotificationsEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "SheilaCameraEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "SheilaCameraProcessingEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "ShortOnLongOff",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "Show24x7Lite",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "ShowOfflineMotionEvents",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "Tracklight",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "VehicleDetectionEligible",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceFeatureStates", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceFeatureReasons", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_DeviceFeatureReasons_DeviceFeatureStates_FeatureStateId",
                        column: x => x.FeatureStateId,
                        principalTable: "DeviceFeatureStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceFeatureReasons_FeatureStateId",
                table: "DeviceFeatureReasons",
                column: "FeatureStateId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceFeatureStates_DeviceId_FeatureKey",
                table: "DeviceFeatureStates",
                columns: new[] { "DeviceId", "FeatureKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "DeviceFeatureReasons");

            _ = migrationBuilder.DropTable(
                name: "DeviceFeatureStates");

            _ = migrationBuilder.DropColumn(
                name: "AlexaPlusGreetings",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "AnimalDetectionEligible",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "AutoShutoffDevice",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "AutoZoomTrack",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "AutomatedSiren",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "CfesEligible",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "DynamicNetworkSwitchingEligible",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "MotionZoneRecommendation",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "PackageWarning",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "PropertyView",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "Recording24x7Eligible",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "RemoteAccessControl",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "RichNotificationsEligible",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "SheilaCameraEligible",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "SheilaCameraProcessingEligible",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "ShortOnLongOff",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "Show24x7Lite",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "ShowOfflineMotionEvents",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "Tracklight",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "VehicleDetectionEligible",
                table: "DeviceFeatures");
        }
    }
}
