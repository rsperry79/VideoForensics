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
            _ = migrationBuilder.AddColumn<decimal>(
                name: "BatteryTempCelsius",
                table: "DeviceReportedHealths",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<DateTime>(
                name: "StatusAtUtc",
                table: "DeviceReportedHealths",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<decimal>(
                name: "BatteryTempCelsius",
                table: "DeviceReportedBatteries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "BatteryTempCelsius",
                table: "DeviceReportedHealths");

            _ = migrationBuilder.DropColumn(
                name: "StatusAtUtc",
                table: "DeviceReportedHealths");

            _ = migrationBuilder.DropColumn(
                name: "BatteryTempCelsius",
                table: "DeviceReportedBatteries");
        }
    }
}
