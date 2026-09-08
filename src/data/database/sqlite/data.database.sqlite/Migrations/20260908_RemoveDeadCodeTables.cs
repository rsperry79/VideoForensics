using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeadCodeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop tables for dead code that was never called in production
            migrationBuilder.DropTable(name: "Annotations");
            migrationBuilder.DropTable(name: "DeviceHealthRecords");
            migrationBuilder.DropTable(name: "ModificationAuditRecords");
            migrationBuilder.DropTable(name: "RedactionAuditRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down migration - recreate tables if needed for rollback
            // Note: This is primarily for safety; rolling back dead code removal is not recommended

            migrationBuilder.CreateTable(
                name: "Annotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Annotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceHealthRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ApiResponseHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceHealthRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModificationAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ModificationType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ChangeSummary = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModificationAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RedactionAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RedactedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ContentRedacted = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    JustificationNotes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    RedactedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedactionAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_EntityType_EntityId",
                table: "Annotations",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_Key_Value",
                table: "Annotations",
                columns: new[] { "Key", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthRecords_DeviceId",
                table: "DeviceHealthRecords",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModificationAuditRecords_EventId",
                table: "ModificationAuditRecords",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ModificationAuditRecords_ModifiedAtUtc",
                table: "ModificationAuditRecords",
                column: "ModifiedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RedactionAuditRecords_EvidenceId",
                table: "RedactionAuditRecords",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_RedactionAuditRecords_RedactedAtUtc",
                table: "RedactionAuditRecords",
                column: "RedactedAtUtc");
        }
    }
}
