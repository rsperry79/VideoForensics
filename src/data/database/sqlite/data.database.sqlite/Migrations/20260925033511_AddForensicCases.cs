using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddForensicCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaseNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LeadOperatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ScopeFromUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ScopeToUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseDevices",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDevices", x => new { x.CaseId, x.DeviceId });
                    table.ForeignKey(
                        name: "FK_CaseDevices_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaseDevices_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MediaItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    AddedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MediaSha256AtAdd = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RemovedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RemovedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RemovalReason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseItems", x => x.Id);
                    table.CheckConstraint("CK_CaseItems_ExactlyOneTarget", "(\"Kind\" = 'Event' AND \"EventId\" IS NOT NULL AND \"MediaItemId\" IS NULL) OR (\"Kind\" = 'Media' AND \"MediaItemId\" IS NOT NULL AND \"EventId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_CaseItems_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaseItems_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseItems_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseDevices_DeviceId",
                table: "CaseDevices",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseItems_CaseId_EventId_Active",
                table: "CaseItems",
                columns: new[] { "CaseId", "EventId" },
                unique: true,
                filter: "\"EventId\" IS NOT NULL AND \"RemovedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseItems_CaseId_MediaItemId_Active",
                table: "CaseItems",
                columns: new[] { "CaseId", "MediaItemId" },
                unique: true,
                filter: "\"MediaItemId\" IS NOT NULL AND \"RemovedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseItems_EventId",
                table: "CaseItems",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseItems_MediaItemId",
                table: "CaseItems",
                column: "MediaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CaseNumber",
                table: "Cases",
                column: "CaseNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseDevices");

            migrationBuilder.DropTable(
                name: "CaseItems");

            migrationBuilder.DropTable(
                name: "Cases");
        }
    }
}
