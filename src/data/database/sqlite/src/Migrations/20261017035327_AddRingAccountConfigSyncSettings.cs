using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRingAccountConfigSyncSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<int>(
                name: "ConfigSyncIntervalHours",
                table: "RingAccounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 24);

            _ = migrationBuilder.AddColumn<DateTime>(
                name: "LastConfigSyncUtc",
                table: "RingAccounts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "ConfigSyncIntervalHours",
                table: "RingAccounts");

            _ = migrationBuilder.DropColumn(
                name: "LastConfigSyncUtc",
                table: "RingAccounts");
        }
    }
}
