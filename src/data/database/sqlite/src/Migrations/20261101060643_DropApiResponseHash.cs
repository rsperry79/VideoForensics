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
            migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "RingAccounts");

            migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "LocationMetadata");

            migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "DeviceCapabilities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "RingAccounts",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "LocationMetadata",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "DeviceCapabilities",
                type: "TEXT",
                maxLength: 256,
                nullable: true);
        }
    }
}
