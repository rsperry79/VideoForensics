using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthConvertedUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BatteryTempCelsius",
                table: "DeviceReportedHealths",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusAtUtc",
                table: "DeviceReportedHealths",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BatteryTempCelsius",
                table: "DeviceReportedBatteries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryTempCelsius",
                table: "DeviceReportedHealths");

            migrationBuilder.DropColumn(
                name: "StatusAtUtc",
                table: "DeviceReportedHealths");

            migrationBuilder.DropColumn(
                name: "BatteryTempCelsius",
                table: "DeviceReportedBatteries");
        }
    }
}
