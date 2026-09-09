using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Delete_OrphanedDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete orphaned devices where LocationId does not exist in Locations table.
            // This maintains referential integrity by removing any Device records that reference non-existent Locations.
            migrationBuilder.Sql(@"
                DELETE FROM Devices
                WHERE LocationId NOT IN (SELECT Id FROM Locations)
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down migration: Note that deleted devices cannot be recovered as their data was permanently removed.
            // This migration is effectively non-reversible due to data loss.
        }
    }
}
