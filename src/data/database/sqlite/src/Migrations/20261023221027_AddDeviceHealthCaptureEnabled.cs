using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceHealthCaptureEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<int>(
                name: "CaptureMode",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            _ = migrationBuilder.AddColumn<bool>(
                name: "HealthCaptureEnabled",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "CaptureMode",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "HealthCaptureEnabled",
                table: "Devices");
        }
    }
}
