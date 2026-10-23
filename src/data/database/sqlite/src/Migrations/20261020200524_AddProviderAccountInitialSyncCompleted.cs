using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderAccountInitialSyncCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<bool>(
                name: "InitialSyncCompleted",
                table: "ProviderAccounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Existing accounts finished their first sync before this flag existed, so they start true and skip the
            // validate-all catch-up. The column default stays false (matches the model); this UPDATE is the backfill.
            _ = migrationBuilder.Sql("UPDATE ProviderAccounts SET InitialSyncCompleted = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "InitialSyncCompleted",
                table: "ProviderAccounts");
        }
    }
}
