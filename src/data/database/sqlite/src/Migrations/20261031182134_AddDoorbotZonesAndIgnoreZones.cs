using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDoorbotZonesAndIgnoreZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AdvancedMotionZonesEnabled",
                table: "RingDeviceSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdvancedMotionZonesType",
                table: "RingDeviceSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RingDeviceAdvancedMotionDetectionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectionType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceAdvancedMotionDetectionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceAdvancedMotionDetectionTypes_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceIgnoreZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ZoneNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceIgnoreZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceIgnoreZones_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceMotionZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceMotionZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceMotionZones_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceIgnoreZoneVertices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingDeviceIgnoreZoneId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: true),
                    Y = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceIgnoreZoneVertices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingDeviceIgnoreZoneVertices_RingDeviceIgnoreZones_RingDeviceIgnoreZoneId",
                        column: x => x.RingDeviceIgnoreZoneId,
                        principalTable: "RingDeviceIgnoreZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceAdvancedMotionDetectionTypes_DeviceId_Position",
                table: "RingDeviceAdvancedMotionDetectionTypes",
                columns: new[] { "DeviceId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceIgnoreZones_DeviceId_ZoneNumber",
                table: "RingDeviceIgnoreZones",
                columns: new[] { "DeviceId", "ZoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceIgnoreZoneVertices_RingDeviceIgnoreZoneId_Position",
                table: "RingDeviceIgnoreZoneVertices",
                columns: new[] { "RingDeviceIgnoreZoneId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceMotionZones_DeviceId_Position",
                table: "RingDeviceMotionZones",
                columns: new[] { "DeviceId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RingDeviceAdvancedMotionDetectionTypes");

            migrationBuilder.DropTable(
                name: "RingDeviceIgnoreZoneVertices");

            migrationBuilder.DropTable(
                name: "RingDeviceMotionZones");

            migrationBuilder.DropTable(
                name: "RingDeviceIgnoreZones");

            migrationBuilder.DropColumn(
                name: "AdvancedMotionZonesEnabled",
                table: "RingDeviceSettings");

            migrationBuilder.DropColumn(
                name: "AdvancedMotionZonesType",
                table: "RingDeviceSettings");
        }
    }
}
