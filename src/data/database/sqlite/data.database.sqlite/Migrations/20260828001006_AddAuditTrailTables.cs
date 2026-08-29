using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTrailTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create AccessAuditLogs table
            migrationBuilder.CreateTable(
                name: "AccessAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AccessedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessAuditLogs", x => x.Id);
                });

            // Create ExportAuditRecords table
            migrationBuilder.CreateTable(
                name: "ExportAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExportedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EventsExported = table.Column<int>(type: "INTEGER", nullable: false),
                    ExportFormat = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportAuditRecords", x => x.Id);
                });

            // Create RedactionAuditRecords table
            migrationBuilder.CreateTable(
                name: "RedactionAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RedactedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RedactedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ContentRedacted = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    JustificationNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedactionAuditRecords", x => x.Id);
                });

            // Create ModificationAuditRecords table
            migrationBuilder.CreateTable(
                name: "ModificationAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ModificationType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ChangeSummary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ApprovedByInvestigator = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModificationAuditRecords", x => x.Id);
                });

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_AccessedAtUtc",
                table: "AccessAuditLogs",
                column: "AccessedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_EvidenceId",
                table: "AccessAuditLogs",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_UserId",
                table: "AccessAuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportAuditRecords_ExportedAtUtc",
                table: "ExportAuditRecords",
                column: "ExportedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExportAuditRecords_LocationId",
                table: "ExportAuditRecords",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_RedactionAuditRecords_EvidenceId",
                table: "RedactionAuditRecords",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_RedactionAuditRecords_RedactedAtUtc",
                table: "RedactionAuditRecords",
                column: "RedactedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ModificationAuditRecords_EventId",
                table: "ModificationAuditRecords",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ModificationAuditRecords_ModifiedAtUtc",
                table: "ModificationAuditRecords",
                column: "ModifiedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessAuditLogs");

            migrationBuilder.DropTable(
                name: "ExportAuditRecords");

            migrationBuilder.DropTable(
                name: "RedactionAuditRecords");

            migrationBuilder.DropTable(
                name: "ModificationAuditRecords");
        }
    }
}
