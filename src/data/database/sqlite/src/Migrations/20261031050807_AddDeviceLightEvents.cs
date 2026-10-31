using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceLightEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LedSecondsRemaining",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LedStatus",
                table: "Devices");

            migrationBuilder.CreateTable(
                name: "DeviceLightEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    IsOn = table.Column<bool>(type: "INTEGER", nullable: false),
                    SecondsRemaining = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLightEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceLightEvents_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLightEvents_DeviceId_OccurredAtUtc",
                table: "DeviceLightEvents",
                columns: new[] { "DeviceId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceLightEvents");

            migrationBuilder.AddColumn<int>(
                name: "LedSecondsRemaining",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LedStatus",
                table: "Devices",
                type: "TEXT",
                nullable: true);
        }
    }
}
