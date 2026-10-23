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
            _ = migrationBuilder.AddColumn<int>(
                name: "AutoBanDistinctAccountThreshold",
                table: "LockoutPolicySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);

            _ = migrationBuilder.AddColumn<int>(
                name: "AutoBanDurationMinutes",
                table: "LockoutPolicySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1440);

            _ = migrationBuilder.AddColumn<bool>(
                name: "AutoBanEnabled",
                table: "LockoutPolicySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            _ = migrationBuilder.AddColumn<int>(
                name: "AutoBanWindowMinutes",
                table: "LockoutPolicySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "AutoBanDistinctAccountThreshold",
                table: "LockoutPolicySettings");

            _ = migrationBuilder.DropColumn(
                name: "AutoBanDurationMinutes",
                table: "LockoutPolicySettings");

            _ = migrationBuilder.DropColumn(
                name: "AutoBanEnabled",
                table: "LockoutPolicySettings");

            _ = migrationBuilder.DropColumn(
                name: "AutoBanWindowMinutes",
                table: "LockoutPolicySettings");
        }
    }
}
