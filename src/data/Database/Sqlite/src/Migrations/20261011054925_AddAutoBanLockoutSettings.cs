using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoBanLockoutSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoBanDistinctAccountThreshold",
                table: "LockoutPolicySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "AutoBanDurationMinutes",
                table: "LockoutPolicySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1440);

            migrationBuilder.AddColumn<bool>(
                name: "AutoBanEnabled",
                table: "LockoutPolicySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AutoBanWindowMinutes",
                table: "LockoutPolicySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoBanDistinctAccountThreshold",
                table: "LockoutPolicySettings");

            migrationBuilder.DropColumn(
                name: "AutoBanDurationMinutes",
                table: "LockoutPolicySettings");

            migrationBuilder.DropColumn(
                name: "AutoBanEnabled",
                table: "LockoutPolicySettings");

            migrationBuilder.DropColumn(
                name: "AutoBanWindowMinutes",
                table: "LockoutPolicySettings");
        }
    }
}
