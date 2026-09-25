using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveViewSessionsAndBitrateBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CameraBitrateBaselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HourOfDay = table.Column<int>(type: "INTEGER", nullable: false),
                    IsWeekend = table.Column<bool>(type: "INTEGER", nullable: false),
                    SampleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MedianBitrateBps = table.Column<long>(type: "INTEGER", nullable: false),
                    StdDevBitrateBps = table.Column<double>(type: "REAL", nullable: false),
                    MedianFractionLost = table.Column<double>(type: "REAL", nullable: false),
                    MedianJitterTicks = table.Column<double>(type: "REAL", nullable: false),
                    LastRecomputedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraBitrateBaselines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveViewSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TriggerReason = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastExtendedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsSustained = table.Column<bool>(type: "INTEGER", nullable: false),
                    SustainedSinceUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PromotionReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StopReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ProviderSessionRef = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveViewSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveViewTelemetrySamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FractionLost = table.Column<byte>(type: "INTEGER", nullable: true),
                    CumulativePacketsLost = table.Column<int>(type: "INTEGER", nullable: true),
                    JitterTicks = table.Column<uint>(type: "INTEGER", nullable: true),
                    BitrateBps = table.Column<long>(type: "INTEGER", nullable: true),
                    InterferenceScore = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveViewTelemetrySamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveViewTelemetrySamples_LiveViewSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "LiveViewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CameraBitrateBaselines_DeviceId_HourOfDay_IsWeekend",
                table: "CameraBitrateBaselines",
                columns: new[] { "DeviceId", "HourOfDay", "IsWeekend" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveViewSessions_DeviceId",
                table: "LiveViewSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveViewSessions_State",
                table: "LiveViewSessions",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_LiveViewTelemetrySamples_SessionId_CapturedAtUtc",
                table: "LiveViewTelemetrySamples",
                columns: new[] { "SessionId", "CapturedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CameraBitrateBaselines");

            migrationBuilder.DropTable(
                name: "LiveViewTelemetrySamples");

            migrationBuilder.DropTable(
                name: "LiveViewSessions");
        }
    }
}
