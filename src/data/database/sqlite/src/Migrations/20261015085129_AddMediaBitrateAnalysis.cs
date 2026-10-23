using System;
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
            migrationBuilder.AddColumn<DateTime>(
                name: "BitrateAnalyzedAtUtc",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BitrateEstimated",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VideoBitrateBps",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BitrateAnalyzedAtUtc",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "BitrateEstimated",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "VideoBitrateBps",
                table: "MediaItems");
        }
    }
}
