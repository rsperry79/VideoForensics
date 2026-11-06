using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceConfigSnapshotSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "CustomSettingsJson",
                table: "DeviceConfigSnapshots");

            _ = migrationBuilder.CreateTable(
                name: "DeviceConfigSnapshotSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ValueJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_DeviceConfigSnapshotSettings", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_DeviceConfigSnapshotSettings_DeviceConfigSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "DeviceConfigSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceConfigSnapshotSettings_SnapshotId_Key",
                table: "DeviceConfigSnapshotSettings",
                columns: new[] { "SnapshotId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "DeviceConfigSnapshotSettings");

            _ = migrationBuilder.AddColumn<string>(
                name: "CustomSettingsJson",
                table: "DeviceConfigSnapshots",
                type: "TEXT",
                nullable: true);
        }
    }
}
