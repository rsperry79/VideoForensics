using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DropDeadJsonColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "RingAccounts");

            _ = migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "LocationMetadata");

            _ = migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "DeviceCapabilities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "RingAccounts",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "LocationMetadata",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "DeviceCapabilities",
                type: "TEXT",
                nullable: true);
        }
    }
}
