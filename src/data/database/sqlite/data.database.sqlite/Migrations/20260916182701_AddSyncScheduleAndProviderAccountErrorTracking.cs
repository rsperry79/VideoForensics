using System;
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
            migrationBuilder.AddColumn<string>(
                name: "LastErrorMessage",
                table: "ProviderAccounts",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastErrorUtc",
                table: "ProviderAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
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
                    table.PrimaryKey("PK_SyncSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
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
                    table.PrimaryKey("PK_JammingScheduleWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JammingScheduleWindows_SyncSchedules_SyncScheduleId",
                        column: x => x.SyncScheduleId,
                        principalTable: "SyncSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JammingScheduleWindows_SyncScheduleId",
                table: "JammingScheduleWindows",
                column: "SyncScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncSchedules_ProviderAccountId",
                table: "SyncSchedules",
                column: "ProviderAccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JammingScheduleWindows");

            migrationBuilder.DropTable(
                name: "SyncSchedules");

            migrationBuilder.DropColumn(
                name: "LastErrorMessage",
                table: "ProviderAccounts");

            migrationBuilder.DropColumn(
                name: "LastErrorUtc",
                table: "ProviderAccounts");
        }
    }
}
