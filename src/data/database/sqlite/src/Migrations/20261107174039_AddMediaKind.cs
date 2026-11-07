using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaItems_DeviceId_RecordedAtUtc",
                table: "MediaItems");

            migrationBuilder.AddColumn<string>(
                name: "MediaKind",
                table: "MediaItems",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Clip");

            // Existing rows get their kind from how they were written: snapshot JPEGs, still images, time-lapse loops, and clips.
            migrationBuilder.Sql(
                """
                UPDATE "MediaItems" SET "MediaKind" = CASE
                    WHEN "MediaFormat" = 'jpg' THEN 'Snapshot'
                    WHEN "MediaFormat" = 'image/png' THEN 'Still'
                    WHEN "FileName" LIKE '%snapshot-loop%' THEN 'Timelapse'
                    ELSE 'Clip'
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_DeviceId_RecordedAtUtc_MediaKind",
                table: "MediaItems",
                columns: new[] { "DeviceId", "RecordedAtUtc", "MediaKind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaItems_DeviceId_RecordedAtUtc_MediaKind",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "MediaKind",
                table: "MediaItems");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_DeviceId_RecordedAtUtc",
                table: "MediaItems",
                columns: new[] { "DeviceId", "RecordedAtUtc" },
                unique: true);
        }
    }
}
