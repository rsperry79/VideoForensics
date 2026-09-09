using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Remove_Location_MetadataJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop Location.MetadataJson column - functionality superseded by LocationMetadata entity
            // which provides structured address, timezone, and coordinate fields.
            // No code populates Location.MetadataJson and no production dependencies exist.
            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "Locations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate Location.MetadataJson column
            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "Locations",
                type: "TEXT",
                nullable: true);
        }
    }
}
