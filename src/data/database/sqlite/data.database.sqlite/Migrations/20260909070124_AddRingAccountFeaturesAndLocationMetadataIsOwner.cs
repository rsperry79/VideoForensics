using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRingAccountFeaturesAndLocationMetadataIsOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RingAccountFeaturesId",
                table: "RingAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOwner",
                table: "LocationMetadata",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RingAccountFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CanViewLiveView = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanViewRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanViewSnapshotsOnly = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanSaveLiveView = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanSaveRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanDeleteRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanPauseRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanShareRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanChangeDeviceSettings = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanActivateAlarmSystem = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccountFeatures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RingAccounts_RingAccountFeaturesId",
                table: "RingAccounts",
                column: "RingAccountFeaturesId");

            migrationBuilder.CreateIndex(
                name: "IX_RingAccountFeatures_RingAccountId",
                table: "RingAccountFeatures",
                column: "RingAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RingAccountFeatures");

            migrationBuilder.DropIndex(
                name: "IX_RingAccounts_RingAccountFeaturesId",
                table: "RingAccounts");

            migrationBuilder.DropColumn(
                name: "RingAccountFeaturesId",
                table: "RingAccounts");

            migrationBuilder.DropColumn(
                name: "IsOwner",
                table: "LocationMetadata");
        }
    }
}
