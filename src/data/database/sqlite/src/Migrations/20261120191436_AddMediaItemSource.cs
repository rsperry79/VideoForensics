using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaItemSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            // Copy the existing source key into the typed column. The JSON keeps its copy: rows written before this
            // change are not rewritten, so the old payload stays available for audit.
            migrationBuilder.Sql(
                "UPDATE MediaItems SET Source = json_extract(MetadataJson, '$.source') " +
                "WHERE MetadataJson IS NOT NULL AND json_valid(MetadataJson) AND json_extract(MetadataJson, '$.source') IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "MediaItems");
        }
    }
}
