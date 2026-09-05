using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceIdToDeviceHealthSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AlterColumn<Guid>(
                name: "DownloadEventId",
                table: "DeviceHealthSnapshots",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            _ = migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "DeviceHealthSnapshots",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthSnapshots_DeviceId_CapturedAtUtc",
                table: "DeviceHealthSnapshots",
                columns: new[] { "DeviceId", "CapturedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropIndex(
                name: "IX_DeviceHealthSnapshots_DeviceId_CapturedAtUtc",
                table: "DeviceHealthSnapshots");

            _ = migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "DeviceHealthSnapshots");

            _ = migrationBuilder.AlterColumn<Guid>(
                name: "DownloadEventId",
                table: "DeviceHealthSnapshots",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
