using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataFieldsToEventAndMediaItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<string>(
                name: "ApiSourceHash",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "ApiSourceHash",
                table: "MediaItems");

            _ = migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "MediaItems");
        }
    }
}
