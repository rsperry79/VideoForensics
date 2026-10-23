using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotCaptures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.CreateTable(
                name: "SnapshotCaptures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    HttpStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    MediaFilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    DeviceHealthId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_SnapshotCaptures", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_SnapshotCaptures_DeviceHealths_DeviceHealthId",
                        column: x => x.DeviceHealthId,
                        principalTable: "DeviceHealths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    _ = table.ForeignKey(
                        name: "FK_SnapshotCaptures_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_SnapshotCaptures_DeviceHealthId",
                table: "SnapshotCaptures",
                column: "DeviceHealthId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_SnapshotCaptures_DeviceId_CapturedAtUtc",
                table: "SnapshotCaptures",
                columns: new[] { "DeviceId", "CapturedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "SnapshotCaptures");
        }
    }
}
