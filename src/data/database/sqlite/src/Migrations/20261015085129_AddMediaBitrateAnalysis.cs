using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaBitrateAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<DateTime>(
                name: "BitrateAnalyzedAtUtc",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "BitrateEstimated",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<long>(
                name: "VideoBitrateBps",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "BitrateAnalyzedAtUtc",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "BitrateEstimated",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "VideoBitrateBps",
                table: "MediaItems");
        }
    }
}
