using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaItemUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add unique index on (DeviceId, RecordedAtUtc) to prevent duplicate downloads on retry/resume
            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_DeviceId_RecordedAtUtc",
                table: "MediaItems",
                columns: new[] { "DeviceId", "RecordedAtUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaItems_DeviceId_RecordedAtUtc",
                table: "MediaItems");
        }
    }
}
