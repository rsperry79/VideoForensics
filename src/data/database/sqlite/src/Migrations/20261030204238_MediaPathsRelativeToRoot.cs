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
            // Why: rows held full paths (<root>\<provider>\<location>\<device>\<file>). The root is configurable and may have no
            // fixed folder name, so keep the trailing segments instead: 3 slashes for a file in a device folder, 4 for one in its
            // "stills" subfolder. Separators become '/'. A path that is not rooted is already converted, so a rerun changes nothing.
            foreach ((string table, string column) in new[] { ("MediaItems", "FilePath"), ("MediaItems", "PreviewPath"), ("SnapshotCaptures", "MediaFilePath") })
            {
                migrationBuilder.Sql($@"
WITH RECURSIVE t(id, rest, keep) AS (
    SELECT Id, replace({column}, char(92), '/'),
           CASE WHEN instr(replace({column}, char(92), '/'), '/stills/') > 0 THEN 4 ELSE 3 END
    FROM {table}
    WHERE {column} IS NOT NULL AND ({column} LIKE '_:%' OR {column} LIKE '/%')
    UNION ALL
    SELECT id, substr(rest, instr(rest, '/') + 1), keep
    FROM t
    WHERE length(rest) - length(replace(rest, '/', '')) > keep
)
UPDATE {table}
SET {column} = (SELECT rest FROM t WHERE t.id = {table}.Id ORDER BY length(rest) LIMIT 1)
WHERE Id IN (SELECT id FROM t);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
