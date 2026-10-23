using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAnalysisFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<int>(
                name: "BaselineVersion",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "ClippedPeakCount",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "JammingSuspected",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "LightingMode",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "MeanLuma",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "MeanSaturation",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "NoiseFloorDb",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "RedFlags",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "SilenceFraction",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "SilentGapCount",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "BaselineVersion",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "ClippedPeakCount",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "JammingSuspected",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "LightingMode",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "MeanLuma",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "MeanSaturation",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "NoiseFloorDb",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "RedFlags",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "SilenceFraction",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "SilentGapCount",
                table: "MediaItems");
        }
    }
}
