using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Add_EventDownloadStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add DownloadStatus column to Events table with default value 0 (NotAttempted)
            migrationBuilder.AddColumn<int>(
                name: "DownloadStatus",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Add DownloadFailedAtUtc column to Events table (nullable)
            migrationBuilder.AddColumn<string>(
                name: "DownloadFailedAtUtc",
                table: "Events",
                type: "TEXT",
                nullable: true);

            // Backfill DownloadStatus based on existing DownloadedAtUtc values
            // SET DownloadStatus = 2 (Downloaded) WHERE DownloadedAtUtc IS NOT NULL
            migrationBuilder.Sql(@"
                UPDATE Events
                SET DownloadStatus = 2
                WHERE DownloadedAtUtc IS NOT NULL
            ");

            // SET DownloadStatus = 0 (NotAttempted) WHERE DownloadedAtUtc IS NULL
            // (This is already the default, but explicit for clarity)
            migrationBuilder.Sql(@"
                UPDATE Events
                SET DownloadStatus = 0
                WHERE DownloadedAtUtc IS NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the new columns
            migrationBuilder.DropColumn(
                name: "DownloadFailedAtUtc",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "DownloadStatus",
                table: "Events");
        }
    }
}
