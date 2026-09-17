using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticesAndPushNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.CreateTable(
                name: "NoticeDismissals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NoticeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DismissedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_NoticeDismissals", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "Notices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Audience = table.Column<int>(type: "INTEGER", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProviderAccountId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_Notices", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "OperatorNotificationPreferences",
                columns: table => new
                {
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PushEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinimumSeverity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_OperatorNotificationPreferences", x => x.OperatorId);
                });

            _ = migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    P256dhKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AuthKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_NoticeDismissals_NoticeId_OperatorId",
                table: "NoticeDismissals",
                columns: new[] { "NoticeId", "OperatorId" },
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions",
                column: "Endpoint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "NoticeDismissals");

            _ = migrationBuilder.DropTable(
                name: "Notices");

            _ = migrationBuilder.DropTable(
                name: "OperatorNotificationPreferences");

            _ = migrationBuilder.DropTable(
                name: "PushSubscriptions");
        }
    }
}
