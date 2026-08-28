using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRingAccountEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionLevel = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Features = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RateLimitPerMinute = table.Column<int>(type: "INTEGER", nullable: true),
                    RateLimitRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    AccountEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AuthenticatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ApiResponseHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RingAccounts_ProviderAccountId",
                table: "RingAccounts",
                column: "ProviderAccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RingAccounts");
        }
    }
}
