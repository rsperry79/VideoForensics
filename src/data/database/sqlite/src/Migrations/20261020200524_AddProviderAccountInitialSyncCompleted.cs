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
            migrationBuilder.AddColumn<bool>(
                name: "InitialSyncCompleted",
                table: "ProviderAccounts",
                type: "INTEGER",
                nullable: false,
                // Existing accounts finished their first sync before this flag existed, so they start true and skip
                // the validate-all catch-up. New accounts set false explicitly in code (EF always writes the value).
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialSyncCompleted",
                table: "ProviderAccounts");
        }
    }
}
