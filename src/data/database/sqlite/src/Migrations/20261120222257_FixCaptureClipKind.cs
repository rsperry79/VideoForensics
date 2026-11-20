using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class FixCaptureClipKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 126 rows are JPEG captures filed as Clip. They match the snapshot rows in every other respect, so they get the
            // Snapshot kind. Their source key is then removed by the same rule as the other capture rows.
            migrationBuilder.Sql(@"
UPDATE MediaItems
SET MediaKind = 'Snapshot'
WHERE MediaKind = 'Clip'
  AND MediaFormat = 'jpg'
  AND json_valid(MetadataJson)
  AND json_extract(MetadataJson, '$.source') = 'capture';");

            migrationBuilder.Sql(@"
UPDATE MediaItems
SET MetadataJson = json_remove(MetadataJson, '$.source')
WHERE MetadataJson IS NOT NULL
  AND json_valid(MetadataJson)
  AND MediaKind = 'Snapshot'
  AND json_extract(MetadataJson, '$.source') = 'capture';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
