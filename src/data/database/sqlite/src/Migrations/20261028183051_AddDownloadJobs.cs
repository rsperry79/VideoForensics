using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DownloadJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Force = table.Column<bool>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinishedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequestedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    FilesDownloaded = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesSkipped = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesFailed = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_ProviderAccountId_CreatedUtc",
                table: "DownloadJobs",
                columns: new[] { "ProviderAccountId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_State_CreatedUtc",
                table: "DownloadJobs",
                columns: new[] { "State", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DownloadJobs");
        }
    }
}
