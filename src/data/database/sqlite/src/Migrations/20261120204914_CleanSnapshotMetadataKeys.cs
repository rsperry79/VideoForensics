using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class CleanSnapshotMetadataKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Snapshot markers written before the typed-column fix still carry keys that are stored elsewhere.
            // deviceId and eventId are removed only when they equal the typed value they duplicate, so no value is lost.
            // timestamp is kept: it is the metadata write time, not MediaItems.RecordedAtUtc, so it is not a duplicate.
            migrationBuilder.Sql(@"
UPDATE MediaItems
SET MetadataJson = json_remove(MetadataJson, '$.deviceId', '$.eventId')
WHERE MetadataJson IS NOT NULL
  AND json_valid(MetadataJson)
  AND json_extract(MetadataJson, '$.type') = 'snapshot'
  AND (json_extract(MetadataJson, '$.eventId') IS NULL OR EXISTS (
        SELECT 1 FROM DownloadEvents d
        WHERE d.Id = MediaItems.DownloadEventId AND d.ProviderEventId = json_extract(MetadataJson, '$.eventId')))
  AND (json_extract(MetadataJson, '$.deviceId') IS NULL OR EXISTS (
        SELECT 1 FROM Devices v
        WHERE v.Id = MediaItems.DeviceId AND v.ProviderDeviceId = json_extract(MetadataJson, '$.deviceId')));");

            // The source key on capture rows repeats MediaKind = Snapshot, which is stored in a typed column.
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
            // Not reversible: the removed keys are not restored. The pre-change backup is the copy to restore from.
        }
    }
}
