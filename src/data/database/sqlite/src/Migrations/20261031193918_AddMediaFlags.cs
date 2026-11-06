using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.CreateTable(
                name: "MediaFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlagType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WindowStartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RaisedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ReviewedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewNote = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    NoticeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_MediaFlags", x => x.Id);
                    _ = table.CheckConstraint("CK_MediaFlags_Window", "\"WindowEndUtc\" >= \"WindowStartUtc\"");
                    _ = table.ForeignKey(
                        name: "FK_MediaFlags_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_MediaFlags_Status_DeviceId_RaisedAtUtc",
                table: "MediaFlags",
                columns: new[] { "Status", "DeviceId", "RaisedAtUtc" });

            _ = migrationBuilder.CreateIndex(
                name: "UX_MediaFlags_DeviceId_FlagType_WindowStartUtc",
                table: "MediaFlags",
                columns: new[] { "DeviceId", "FlagType", "WindowStartUtc" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "UX_MediaFlags_MediaItemId_FlagType",
                table: "MediaFlags",
                columns: new[] { "MediaItemId", "FlagType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "MediaFlags");
        }
    }
}
