using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceTypedSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceDetectionThresholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DetectionType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Threshold = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceDetectionThresholds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceDetectionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DetectionType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Record = table.Column<bool>(type: "INTEGER", nullable: true),
                    Notification = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceDetectionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceOfflineMotionSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Subscribed = table.Column<bool>(type: "INTEGER", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    MaxUploadKb = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolutionP = table.Column<int>(type: "INTEGER", nullable: true),
                    FrequencyAfterSecs = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodAfterSecs = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceOfflineMotionSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceDetectionThresholds_DeviceId",
                table: "DeviceDetectionThresholds",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceDetectionTypes_DeviceId",
                table: "DeviceDetectionTypes",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceOfflineMotionSettings_DeviceId",
                table: "DeviceOfflineMotionSettings",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceDetectionThresholds");

            migrationBuilder.DropTable(
                name: "DeviceDetectionTypes");

            migrationBuilder.DropTable(
                name: "DeviceOfflineMotionSettings");
        }
    }
}
