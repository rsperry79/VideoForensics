using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceBaselineDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BaselineDriftChangeFraction",
                table: "Devices",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BaselineDriftCheckedAtUtc",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BaselineDriftDetected",
                table: "Devices",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaselineDriftChangeFraction",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "BaselineDriftCheckedAtUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "BaselineDriftDetected",
                table: "Devices");
        }
    }
}
