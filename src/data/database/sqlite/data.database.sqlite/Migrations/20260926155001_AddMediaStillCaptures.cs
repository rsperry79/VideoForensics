using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaStillCaptures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaStillCaptures",
                columns: table => new
                {
                    MediaItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceMediaItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FrameOffsetMs = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceSha256AtCapture = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CapturedByOperator = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CaptureMethod = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaStillCaptures", x => x.MediaItemId);
                    table.ForeignKey(
                        name: "FK_MediaStillCaptures_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaStillCaptures_MediaItems_SourceMediaItemId",
                        column: x => x.SourceMediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaStillCaptures_SourceMediaItemId",
                table: "MediaStillCaptures",
                column: "SourceMediaItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaStillCaptures");
        }
    }
}
