using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DropOperationalLogTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "LiveViewTelemetrySamples");

            _ = migrationBuilder.DropTable(
                name: "ProviderApiCallRecords");

            _ = migrationBuilder.DropTable(
                name: "ProviderApiErrorLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.CreateTable(
                name: "LiveViewTelemetrySamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BitrateBps = table.Column<long>(type: "INTEGER", nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CumulativePacketsLost = table.Column<int>(type: "INTEGER", nullable: true),
                    FractionLost = table.Column<byte>(type: "INTEGER", nullable: true),
                    InterferenceScore = table.Column<double>(type: "REAL", nullable: true),
                    JitterTicks = table.Column<uint>(type: "INTEGER", nullable: true),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_LiveViewTelemetrySamples", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_LiveViewTelemetrySamples_LiveViewSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "LiveViewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
                name: "ProviderApiCallRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_ProviderApiCallRecords", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "ProviderApiErrorLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ErrorCategory = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExceptionType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    HttpMethod = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RequestUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_ProviderApiErrorLogs", x => x.Id);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_LiveViewTelemetrySamples_SessionId_CapturedAtUtc",
                table: "LiveViewTelemetrySamples",
                columns: new[] { "SessionId", "CapturedAtUtc" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProviderApiCallRecords_ProviderName_TimestampUtc",
                table: "ProviderApiCallRecords",
                columns: new[] { "ProviderName", "TimestampUtc" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProviderApiErrorLogs_DeviceId_OccurredAtUtc",
                table: "ProviderApiErrorLogs",
                columns: new[] { "DeviceId", "OccurredAtUtc" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProviderApiErrorLogs_EventId",
                table: "ProviderApiErrorLogs",
                column: "EventId");
        }
    }
}
