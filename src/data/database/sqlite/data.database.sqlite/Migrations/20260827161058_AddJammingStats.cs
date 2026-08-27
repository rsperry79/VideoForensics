using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddJammingStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JammingIncidentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AffectedEventCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageDegradationDb = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JammingIncidentRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JammingStatsSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IncidentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalJammedDurationMinutes = table.Column<double>(type: "REAL", nullable: false),
                    AverageDegradationDb = table.Column<double>(type: "REAL", nullable: false),
                    MaxDegradationDb = table.Column<double>(type: "REAL", nullable: false),
                    LowConfidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MediumConfidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HighConfidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DefiniteConfidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstIncidentUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastIncidentUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JammingStatsSummaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JammingIncidentRecords_DeviceId",
                table: "JammingIncidentRecords",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_JammingStatsSummaries_DeviceId",
                table: "JammingStatsSummaries",
                column: "DeviceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JammingIncidentRecords");

            migrationBuilder.DropTable(
                name: "JammingStatsSummaries");
        }
    }
}
