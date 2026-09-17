using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncScheduleAndProviderAccountErrorTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<string>(
                name: "LastErrorMessage",
                table: "ProviderAccounts",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            _ = migrationBuilder.AddColumn<DateTime>(
                name: "LastErrorUtc",
                table: "ProviderAccounts",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.CreateTable(
                name: "SyncSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventPollIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotRssiIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseAdvancedJammingSchedule = table.Column<bool>(type: "INTEGER", nullable: false),
                    EventNextRunUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EventLastRunUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SnapshotNextRunUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SnapshotLastRunUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_SyncSchedules", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "JammingScheduleWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncScheduleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DayOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    StartMinuteOfDay = table.Column<int>(type: "INTEGER", nullable: false),
                    EndMinuteOfDay = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotRssiIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_JammingScheduleWindows", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_JammingScheduleWindows_SyncSchedules_SyncScheduleId",
                        column: x => x.SyncScheduleId,
                        principalTable: "SyncSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_JammingScheduleWindows_SyncScheduleId",
                table: "JammingScheduleWindows",
                column: "SyncScheduleId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_SyncSchedules_ProviderAccountId",
                table: "SyncSchedules",
                column: "ProviderAccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "JammingScheduleWindows");

            _ = migrationBuilder.DropTable(
                name: "SyncSchedules");

            _ = migrationBuilder.DropColumn(
                name: "LastErrorMessage",
                table: "ProviderAccounts");

            _ = migrationBuilder.DropColumn(
                name: "LastErrorUtc",
                table: "ProviderAccounts");
        }
    }
}
