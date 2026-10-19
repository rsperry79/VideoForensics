using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceSnapshotCaptureEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing devices default to capture on, matching the entity default.
            migrationBuilder.AddColumn<bool>(
                name: "SnapshotCaptureEnabled",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotCaptureEnabled",
                table: "Devices");
        }
    }
}
