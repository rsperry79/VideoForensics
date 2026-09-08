using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <summary>
    /// Migration to make Location.ProviderLocationId globally unique.
    /// Removes the ProviderAccountId column and changes the unique constraint from
    /// (ProviderAccountId, ProviderLocationId) to just ProviderLocationId.
    /// </summary>
    public partial class MakeLocationProviderLocationIdGloballyUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop the existing indices that reference ProviderAccountId
            migrationBuilder.DropIndex(
                name: "IX_Locations_ProviderAccountId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_ProviderAccountId_ProviderLocationId",
                table: "Locations");

            // Step 2: Drop the ProviderAccountId column
            migrationBuilder.DropColumn(
                name: "ProviderAccountId",
                table: "Locations");

            // Step 3: Create unique index on ProviderLocationId only
            migrationBuilder.CreateIndex(
                name: "IX_Locations_ProviderLocationId",
                table: "Locations",
                column: "ProviderLocationId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: Drop the unique index on ProviderLocationId
            migrationBuilder.DropIndex(
                name: "IX_Locations_ProviderLocationId",
                table: "Locations");

            // Rollback: Re-add the ProviderAccountId column (this cannot restore the original data mapping,
            // so this migration is partially irreversible)
            migrationBuilder.AddColumn<Guid>(
                name: "ProviderAccountId",
                table: "Locations",
                type: "TEXT",
                nullable: false,
                defaultValue: Guid.Empty);

            // Rollback: Recreate the composite unique index
            migrationBuilder.CreateIndex(
                name: "IX_Locations_ProviderAccountId_ProviderLocationId",
                table: "Locations",
                columns: new[] { "ProviderAccountId", "ProviderLocationId" },
                unique: true);

            // Rollback: Recreate the index on ProviderAccountId
            migrationBuilder.CreateIndex(
                name: "IX_Locations_ProviderAccountId",
                table: "Locations",
                column: "ProviderAccountId");
        }
    }
}
