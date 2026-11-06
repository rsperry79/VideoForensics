using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class GenericNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows keep their text, author and time. Each becomes a Case note because the default is "Case".
            // The FK and the old index go before the column rename so SQLite rebuilds the table against the old names.
            _ = migrationBuilder.RenameTable(
                name: "CaseNotes",
                newName: "Notes");

            _ = migrationBuilder.DropForeignKey(
                name: "FK_CaseNotes_Cases_CaseId",
                table: "Notes");

            _ = migrationBuilder.DropIndex(
                name: "IX_CaseNotes_CaseId_AddedAtUtc",
                table: "Notes");

            _ = migrationBuilder.RenameColumn(
                name: "CaseId",
                table: "Notes",
                newName: "TargetId");

            _ = migrationBuilder.AddColumn<string>(
                name: "TargetKind",
                table: "Notes",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Case");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Notes_TargetKind_TargetId_AddedAtUtc",
                table: "Notes",
                columns: new[] { "TargetKind", "TargetId", "AddedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only case notes can go back to CaseNotes. Jamming and flag notes have no place in the old table, so they are removed.
            _ = migrationBuilder.Sql("DELETE FROM Notes WHERE TargetKind <> 'Case';");

            _ = migrationBuilder.DropIndex(
                name: "IX_Notes_TargetKind_TargetId_AddedAtUtc",
                table: "Notes");

            _ = migrationBuilder.DropColumn(
                name: "TargetKind",
                table: "Notes");

            _ = migrationBuilder.RenameColumn(
                name: "TargetId",
                table: "Notes",
                newName: "CaseId");

            _ = migrationBuilder.AddForeignKey(
                name: "FK_CaseNotes_Cases_CaseId",
                table: "Notes",
                column: "CaseId",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            _ = migrationBuilder.CreateIndex(
                name: "IX_CaseNotes_CaseId_AddedAtUtc",
                table: "Notes",
                columns: new[] { "CaseId", "AddedAtUtc" });

            _ = migrationBuilder.RenameTable(
                name: "Notes",
                newName: "CaseNotes");
        }
    }
}
