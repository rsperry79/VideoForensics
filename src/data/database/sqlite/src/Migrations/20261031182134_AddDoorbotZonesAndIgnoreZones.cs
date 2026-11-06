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
            _ = migrationBuilder.AddColumn<bool>(
                name: "AdvancedMotionZonesEnabled",
                table: "RingDeviceSettings",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "AdvancedMotionZonesType",
                table: "RingDeviceSettings",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_RingDeviceAdvancedMotionDetectionTypes", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_RingDeviceAdvancedMotionDetectionTypes_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_RingDeviceIgnoreZones", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_RingDeviceIgnoreZones_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_RingDeviceMotionZones", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_RingDeviceMotionZones_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_RingDeviceIgnoreZoneVertices", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_RingDeviceIgnoreZoneVertices_RingDeviceIgnoreZones_RingDeviceIgnoreZoneId",
                        column: x => x.RingDeviceIgnoreZoneId,
                        principalTable: "RingDeviceIgnoreZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_RingDeviceAdvancedMotionDetectionTypes_DeviceId_Position",
                table: "RingDeviceAdvancedMotionDetectionTypes",
                columns: new[] { "DeviceId", "Position" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_RingDeviceIgnoreZones_DeviceId_ZoneNumber",
                table: "RingDeviceIgnoreZones",
                columns: new[] { "DeviceId", "ZoneNumber" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_RingDeviceIgnoreZoneVertices_RingDeviceIgnoreZoneId_Position",
                table: "RingDeviceIgnoreZoneVertices",
                columns: new[] { "RingDeviceIgnoreZoneId", "Position" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_RingDeviceMotionZones_DeviceId_Position",
                table: "RingDeviceMotionZones",
                columns: new[] { "DeviceId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "RingDeviceAdvancedMotionDetectionTypes");

            _ = migrationBuilder.DropTable(
                name: "RingDeviceIgnoreZoneVertices");

            _ = migrationBuilder.DropTable(
                name: "RingDeviceMotionZones");

            _ = migrationBuilder.DropTable(
                name: "RingDeviceIgnoreZones");

            _ = migrationBuilder.DropColumn(
                name: "AdvancedMotionZonesEnabled",
                table: "RingDeviceSettings");

            _ = migrationBuilder.DropColumn(
                name: "AdvancedMotionZonesType",
                table: "RingDeviceSettings");
        }
    }
}
