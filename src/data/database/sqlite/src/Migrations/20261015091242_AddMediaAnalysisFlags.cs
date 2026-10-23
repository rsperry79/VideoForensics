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
            migrationBuilder.AddColumn<int>(
                name: "BaselineVersion",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClippedPeakCount",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "JammingSuspected",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LightingMode",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MeanLuma",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MeanSaturation",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NoiseFloorDb",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedFlags",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SilenceFraction",
                table: "MediaItems",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SilentGapCount",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaselineVersion",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ClippedPeakCount",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "JammingSuspected",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "LightingMode",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "MeanLuma",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "MeanSaturation",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "NoiseFloorDb",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "RedFlags",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "SilenceFraction",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "SilentGapCount",
                table: "MediaItems");
        }
    }
}
