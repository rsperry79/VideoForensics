using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceDataCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.CreateTable(
                name: "DeviceFeatureDefinitions",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_DeviceFeatureDefinitions", x => x.Code);
                });

            _ = migrationBuilder.CreateTable(
                name: "DeviceModels",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    Family = table.Column<string>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", nullable: true),
                    Generation = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_DeviceModels", x => x.Kind);
                });

            _ = migrationBuilder.CreateTable(
                name: "ReferenceDataVersions",
                columns: table => new
                {
                    SourceName = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    AppliedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_ReferenceDataVersions", x => x.SourceName);
                });

            _ = migrationBuilder.CreateTable(
                name: "DeviceModelFeatures",
                columns: table => new
                {
                    DeviceModelKind = table.Column<string>(type: "TEXT", nullable: false),
                    FeatureCode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_DeviceModelFeatures", x => new { x.DeviceModelKind, x.FeatureCode });
                    _ = table.ForeignKey(
                        name: "FK_DeviceModelFeatures_DeviceFeatureDefinitions_FeatureCode",
                        column: x => x.FeatureCode,
                        principalTable: "DeviceFeatureDefinitions",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    _ = table.ForeignKey(
                        name: "FK_DeviceModelFeatures_DeviceModels_DeviceModelKind",
                        column: x => x.DeviceModelKind,
                        principalTable: "DeviceModels",
                        principalColumn: "Kind",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceModelFeatures_FeatureCode",
                table: "DeviceModelFeatures",
                column: "FeatureCode");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceModels_Provider",
                table: "DeviceModels",
                column: "Provider");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "DeviceModelFeatures");

            _ = migrationBuilder.DropTable(
                name: "ReferenceDataVersions");

            _ = migrationBuilder.DropTable(
                name: "DeviceFeatureDefinitions");

            _ = migrationBuilder.DropTable(
                name: "DeviceModels");
        }
    }
}
