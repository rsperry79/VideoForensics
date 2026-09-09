using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Drop_UnusedEmptyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop SecurityAlert table (empty, all rows deleted during schema normalization)
            migrationBuilder.DropTable(
                name: "SecurityAlerts");

            // Drop DetectionZone table (empty, no longer used)
            migrationBuilder.DropTable(
                name: "DetectionZones");

            // Drop DeviceLocation table (empty, functionality moved to Location and Device tables)
            migrationBuilder.DropTable(
                name: "DeviceLocations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate SecurityAlerts table
            migrationBuilder.CreateTable(
                name: "SecurityAlerts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: true),
                    DownloadEventId = table.Column<string>(type: "TEXT", nullable: true),
                    AlertType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DetectedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAlerts", x => x.Id);
                });

            // Recreate DetectionZones table
            migrationBuilder.CreateTable(
                name: "DetectionZones",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: true),
                    ZoneName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PolygonCoordinates = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetectionZones", x => x.Id);
                });

            // Recreate DeviceLocations table
            migrationBuilder.CreateTable(
                name: "DeviceLocations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: true),
                    LocationId = table.Column<string>(type: "TEXT", nullable: true),
                    AssignedAtUtc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLocations", x => x.Id);
                });
        }
    }
}
