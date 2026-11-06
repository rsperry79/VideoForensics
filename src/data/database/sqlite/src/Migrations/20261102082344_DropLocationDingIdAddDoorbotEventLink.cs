using System;
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
            migrationBuilder.DropColumn(
                name: "DingId",
                table: "RingLocationEvents");

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedEventId",
                table: "RingDoorbotHistoryEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDoorbotHistoryEvents_LinkedEventId",
                table: "RingDoorbotHistoryEvents",
                column: "LinkedEventId");

            migrationBuilder.AddForeignKey(
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
            migrationBuilder.DropForeignKey(
                name: "FK_RingDoorbotHistoryEvents_Events_LinkedEventId",
                table: "RingDoorbotHistoryEvents");

            migrationBuilder.DropIndex(
                name: "IX_RingDoorbotHistoryEvents_LinkedEventId",
                table: "RingDoorbotHistoryEvents");

            migrationBuilder.DropColumn(
                name: "LinkedEventId",
                table: "RingDoorbotHistoryEvents");

            migrationBuilder.AddColumn<long>(
                name: "DingId",
                table: "RingLocationEvents",
                type: "INTEGER",
                nullable: true);
        }
    }
}
