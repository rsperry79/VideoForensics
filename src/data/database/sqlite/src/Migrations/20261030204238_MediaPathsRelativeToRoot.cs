using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MediaPathsRelativeToRoot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Why: rows held full paths under <root>\media\<provider>\<location>\<device>. They now hold the part after the
            // "media" folder, with '/' separators, relative to the media root. A row with no "media" folder is left alone.
            foreach (string column in new[] { "FilePath", "PreviewPath" })
            {
                migrationBuilder.Sql(
                    $"UPDATE MediaItems SET {column} = replace(substr({column}, instr(lower(replace({column}, '\', '/')), '/media/') + 7), '\', '/') " +
                    $"WHERE {column} IS NOT NULL AND instr(lower(replace({column}, '\', '/')), '/media/') > 0;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
