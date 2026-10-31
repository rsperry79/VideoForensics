using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RenameDeviceFeatureStatesToRing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep rows: rename in place (hand-edited from the scaffolded drop/create). SQLite rewrites the reasons table's FK
            // to the renamed parent; the PK/FK constraint names inside the table DDL keep their old text, which EF never reads.
            migrationBuilder.DropIndex(name: "IX_DeviceFeatureReasons_FeatureStateId", table: "DeviceFeatureReasons");
            migrationBuilder.DropIndex(name: "IX_DeviceFeatureStates_DeviceId_FeatureKey", table: "DeviceFeatureStates");
            migrationBuilder.RenameTable(name: "DeviceFeatureReasons", newName: "RingDeviceFeatureReasons");
            migrationBuilder.RenameTable(name: "DeviceFeatureStates", newName: "RingDeviceFeatureStates");
            migrationBuilder.CreateIndex(name: "IX_RingDeviceFeatureReasons_FeatureStateId", table: "RingDeviceFeatureReasons", column: "FeatureStateId");
            migrationBuilder.CreateIndex(name: "IX_RingDeviceFeatureStates_DeviceId_FeatureKey", table: "RingDeviceFeatureStates", columns: new[] { "DeviceId", "FeatureKey" }, unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep rows: rename in place (hand-edited from the scaffolded drop/create). SQLite rewrites the reasons table's FK
            // to the renamed parent; the PK/FK constraint names inside the table DDL keep their old text, which EF never reads.
            migrationBuilder.DropIndex(name: "IX_RingDeviceFeatureReasons_FeatureStateId", table: "RingDeviceFeatureReasons");
            migrationBuilder.DropIndex(name: "IX_RingDeviceFeatureStates_DeviceId_FeatureKey", table: "RingDeviceFeatureStates");
            migrationBuilder.RenameTable(name: "RingDeviceFeatureReasons", newName: "DeviceFeatureReasons");
            migrationBuilder.RenameTable(name: "RingDeviceFeatureStates", newName: "DeviceFeatureStates");
            migrationBuilder.CreateIndex(name: "IX_DeviceFeatureReasons_FeatureStateId", table: "DeviceFeatureReasons", column: "FeatureStateId");
            migrationBuilder.CreateIndex(name: "IX_DeviceFeatureStates_DeviceId_FeatureKey", table: "DeviceFeatureStates", columns: new[] { "DeviceId", "FeatureKey" }, unique: true);
        }
    }
}
