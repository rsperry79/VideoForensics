using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderAccountLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderAccountLocations",
                columns: table => new
                {
                    ProviderAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderAccountLocations", x => new { x.ProviderAccountId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_ProviderAccountLocations_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderAccountLocations_ProviderAccounts_ProviderAccountId",
                        column: x => x.ProviderAccountId,
                        principalTable: "ProviderAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Locations were not account-scoped before, so link every existing location to every
            // existing account to preserve current pull behavior.
            migrationBuilder.Sql(
                "INSERT INTO ProviderAccountLocations (ProviderAccountId, LocationId) SELECT a.Id, l.Id FROM ProviderAccounts a CROSS JOIN Locations l;");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAccountLocations_LocationId",
                table: "ProviderAccountLocations",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderAccountLocations");
        }
    }
}
