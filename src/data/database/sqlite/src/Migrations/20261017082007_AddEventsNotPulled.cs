using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddEventsNotPulled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<int>(
                name: "EventsAlreadyPresent",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "EventsNotPulled",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "EventsAlreadyPresent",
                table: "ProviderSyncRuns");

            _ = migrationBuilder.DropColumn(
                name: "EventsNotPulled",
                table: "ProviderSyncRuns");
        }
    }
}
