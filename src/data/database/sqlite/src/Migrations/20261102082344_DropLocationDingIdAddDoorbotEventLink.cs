using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DropLocationDingIdAddDoorbotEventLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "DingId",
                table: "RingLocationEvents");

            _ = migrationBuilder.AddColumn<Guid>(
                name: "LinkedEventId",
                table: "RingDoorbotHistoryEvents",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_RingDoorbotHistoryEvents_LinkedEventId",
                table: "RingDoorbotHistoryEvents",
                column: "LinkedEventId");

            _ = migrationBuilder.AddForeignKey(
                name: "FK_RingDoorbotHistoryEvents_Events_LinkedEventId",
                table: "RingDoorbotHistoryEvents",
                column: "LinkedEventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropForeignKey(
                name: "FK_RingDoorbotHistoryEvents_Events_LinkedEventId",
                table: "RingDoorbotHistoryEvents");

            _ = migrationBuilder.DropIndex(
                name: "IX_RingDoorbotHistoryEvents_LinkedEventId",
                table: "RingDoorbotHistoryEvents");

            _ = migrationBuilder.DropColumn(
                name: "LinkedEventId",
                table: "RingDoorbotHistoryEvents");

            _ = migrationBuilder.AddColumn<long>(
                name: "DingId",
                table: "RingLocationEvents",
                type: "INTEGER",
                nullable: true);
        }
    }
}
