using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseItemSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropCheckConstraint(
                name: "CK_CaseItems_ExactlyOneTarget",
                table: "CaseItems");

            _ = migrationBuilder.AddColumn<Guid>(
                name: "SnapshotCaptureId",
                table: "CaseItems",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_CaseItems_CaseId_SnapshotCaptureId_Active",
                table: "CaseItems",
                columns: new[] { "CaseId", "SnapshotCaptureId" },
                unique: true,
                filter: "\"SnapshotCaptureId\" IS NOT NULL AND \"RemovedAtUtc\" IS NULL");

            _ = migrationBuilder.CreateIndex(
                name: "IX_CaseItems_SnapshotCaptureId",
                table: "CaseItems",
                column: "SnapshotCaptureId");

            _ = migrationBuilder.AddCheckConstraint(
                name: "CK_CaseItems_ExactlyOneTarget",
                table: "CaseItems",
                sql: "(\"Kind\" = 'Event' AND \"EventId\" IS NOT NULL AND \"MediaItemId\" IS NULL AND \"SnapshotCaptureId\" IS NULL) OR (\"Kind\" = 'Media' AND \"MediaItemId\" IS NOT NULL AND \"EventId\" IS NULL AND \"SnapshotCaptureId\" IS NULL) OR (\"Kind\" = 'Snapshot' AND \"SnapshotCaptureId\" IS NOT NULL AND \"EventId\" IS NULL AND \"MediaItemId\" IS NULL)");

            _ = migrationBuilder.AddForeignKey(
                name: "FK_CaseItems_SnapshotCaptures_SnapshotCaptureId",
                table: "CaseItems",
                column: "SnapshotCaptureId",
                principalTable: "SnapshotCaptures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropForeignKey(
                name: "FK_CaseItems_SnapshotCaptures_SnapshotCaptureId",
                table: "CaseItems");

            _ = migrationBuilder.DropIndex(
                name: "IX_CaseItems_CaseId_SnapshotCaptureId_Active",
                table: "CaseItems");

            _ = migrationBuilder.DropIndex(
                name: "IX_CaseItems_SnapshotCaptureId",
                table: "CaseItems");

            _ = migrationBuilder.DropCheckConstraint(
                name: "CK_CaseItems_ExactlyOneTarget",
                table: "CaseItems");

            _ = migrationBuilder.DropColumn(
                name: "SnapshotCaptureId",
                table: "CaseItems");

            _ = migrationBuilder.AddCheckConstraint(
                name: "CK_CaseItems_ExactlyOneTarget",
                table: "CaseItems",
                sql: "(\"Kind\" = 'Event' AND \"EventId\" IS NOT NULL AND \"MediaItemId\" IS NULL) OR (\"Kind\" = 'Media' AND \"MediaItemId\" IS NOT NULL AND \"EventId\" IS NULL)");
        }
    }
}
