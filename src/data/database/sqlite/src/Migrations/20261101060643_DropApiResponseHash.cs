using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DropApiResponseHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "RingAccounts");

            _ = migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Locations");

            _ = migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "LocationMetadata");

            _ = migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "DeviceCapabilities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "RingAccounts",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "LocationMetadata",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "DeviceCapabilities",
                type: "TEXT",
                maxLength: 256,
                nullable: true);
        }
    }
}
