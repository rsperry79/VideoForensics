using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSmallEndpointTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStatusInitiatingEntityId",
                table: "RingLocationModes",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CekTransferRecipientResponse",
                table: "RingLocationHistoryPages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CekTransferRecipientResponse",
                table: "RingLocationHistoryEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MapVisualization",
                table: "RingLocationHistoryEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PotentialGapVisualization",
                table: "RingLocationHistoryEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RadarVisualization",
                table: "RingLocationHistoryEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeyMoments",
                table: "RingEventCvProperties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjectsPerLens",
                table: "RingEventCvDetectionDetails",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatteryHatchOpen",
                table: "RingDeviceHealthExtras",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatteryPercentage2",
                table: "RingDeviceHealthExtras",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatteryVoltage2",
                table: "RingDeviceHealthExtras",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CrossStreet",
                table: "LocationMetadata",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress2",
                table: "LocationMetadata",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RingChimeLinkedDoorbots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChimeDeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedDoorbotId = table.Column<long>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CallAlerts = table.Column<bool>(type: "INTEGER", nullable: true),
                    MotionAlerts = table.Column<bool>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingChimeLinkedDoorbots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingChimeLinkedDoorbots_Devices_ChimeDeviceId",
                        column: x => x.ChimeDeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<long>(type: "INTEGER", nullable: true),
                    BusinessCommandCenter = table.Column<bool>(type: "INTEGER", nullable: true),
                    GuardPatrol = table.Column<bool>(type: "INTEGER", nullable: true),
                    PrivateGuardResponse = table.Column<bool>(type: "INTEGER", nullable: true),
                    VmVideoVerification = table.Column<bool>(type: "INTEGER", nullable: true),
                    VmVirtualSecurityGuard = table.Column<bool>(type: "INTEGER", nullable: true),
                    UserVerified = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsJobsite = table.Column<bool>(type: "INTEGER", nullable: true),
                    GeoServiceVerified = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LocationType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LocationResourceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationDetails_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationSharedUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Verified = table.Column<bool>(type: "INTEGER", nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ObjectType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationSharedUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationSharedUsers_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingSnapshotTimestamps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DoorbotId = table.Column<long>(type: "INTEGER", nullable: false),
                    TimestampEpoch = table.Column<long>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingSnapshotTimestamps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingSnapshotTimestamps_RingAccounts_RingAccountId",
                        column: x => x.RingAccountId,
                        principalTable: "RingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationSharedUserDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingLocationSharedUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderDeviceId = table.Column<long>(type: "INTEGER", nullable: false),
                    DeviceType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationSharedUserDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationSharedUserDevices_RingLocationSharedUsers_RingLocationSharedUserId",
                        column: x => x.RingLocationSharedUserId,
                        principalTable: "RingLocationSharedUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingLocationSharedUserDevicePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingLocationSharedUserDeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingLocationSharedUserDevicePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingLocationSharedUserDevicePermissions_RingLocationSharedUserDevices_RingLocationSharedUserDeviceId",
                        column: x => x.RingLocationSharedUserDeviceId,
                        principalTable: "RingLocationSharedUserDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RingChimeLinkedDoorbots_ChimeDeviceId_LinkedDoorbotId",
                table: "RingChimeLinkedDoorbots",
                columns: new[] { "ChimeDeviceId", "LinkedDoorbotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationDetails_LocationId",
                table: "RingLocationDetails",
                column: "LocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationSharedUserDevicePermissions_RingLocationSharedUserDeviceId_Position",
                table: "RingLocationSharedUserDevicePermissions",
                columns: new[] { "RingLocationSharedUserDeviceId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationSharedUserDevices_RingLocationSharedUserId_Position",
                table: "RingLocationSharedUserDevices",
                columns: new[] { "RingLocationSharedUserId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingLocationSharedUsers_LocationId_ProviderUserId",
                table: "RingLocationSharedUsers",
                columns: new[] { "LocationId", "ProviderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingSnapshotTimestamps_RingAccountId_DoorbotId",
                table: "RingSnapshotTimestamps",
                columns: new[] { "RingAccountId", "DoorbotId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RingChimeLinkedDoorbots");

            migrationBuilder.DropTable(
                name: "RingLocationDetails");

            migrationBuilder.DropTable(
                name: "RingLocationSharedUserDevicePermissions");

            migrationBuilder.DropTable(
                name: "RingSnapshotTimestamps");

            migrationBuilder.DropTable(
                name: "RingLocationSharedUserDevices");

            migrationBuilder.DropTable(
                name: "RingLocationSharedUsers");

            migrationBuilder.DropColumn(
                name: "SecurityStatusInitiatingEntityId",
                table: "RingLocationModes");

            migrationBuilder.DropColumn(
                name: "CekTransferRecipientResponse",
                table: "RingLocationHistoryPages");

            migrationBuilder.DropColumn(
                name: "CekTransferRecipientResponse",
                table: "RingLocationHistoryEvents");

            migrationBuilder.DropColumn(
                name: "MapVisualization",
                table: "RingLocationHistoryEvents");

            migrationBuilder.DropColumn(
                name: "PotentialGapVisualization",
                table: "RingLocationHistoryEvents");

            migrationBuilder.DropColumn(
                name: "RadarVisualization",
                table: "RingLocationHistoryEvents");

            migrationBuilder.DropColumn(
                name: "KeyMoments",
                table: "RingEventCvProperties");

            migrationBuilder.DropColumn(
                name: "ObjectsPerLens",
                table: "RingEventCvDetectionDetails");

            migrationBuilder.DropColumn(
                name: "BatteryHatchOpen",
                table: "RingDeviceHealthExtras");

            migrationBuilder.DropColumn(
                name: "BatteryPercentage2",
                table: "RingDeviceHealthExtras");

            migrationBuilder.DropColumn(
                name: "BatteryVoltage2",
                table: "RingDeviceHealthExtras");

            migrationBuilder.DropColumn(
                name: "CrossStreet",
                table: "LocationMetadata");

            migrationBuilder.DropColumn(
                name: "StreetAddress2",
                table: "LocationMetadata");
        }
    }
}
