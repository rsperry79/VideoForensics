using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorAuthAndCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalFirstLoginNotifiedAtUtc",
                table: "Operators",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Operators",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Operators",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Operators",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Operators",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Operators",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordUpdatedAtUtc",
                table: "Operators",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Operators",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Operators",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                table: "Operators",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Operators",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            // Backfill unique Username/Email for any pre-existing Operator rows before the unique
            // indexes below are created - the AddColumn defaultValue above ("") would otherwise give
            // every existing row the SAME Username/Email, which fails to create a unique index (or
            // silently "succeeds" with meaningless duplicate-looking data) for any DB with more than
            // one operator already in it. Id is already unique per row, so deriving Username/Email
            // from it guarantees uniqueness without needing a random-value SQL function.
            migrationBuilder.Sql(@"
                UPDATE Operators
                SET Username = 'operator-' || lower(replace(Id, '-', '')),
                    Email = lower(replace(Id, '-', '')) || '@migrated.invalid'
                WHERE Username = '';
            ");

            migrationBuilder.CreateTable(
                name: "OperatorCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    WebAuthnCredentialId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    WebAuthnPublicKey = table.Column<byte[]>(type: "BLOB", nullable: false),
                    WebAuthnSignCount = table.Column<uint>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsApproved = table.Column<bool>(type: "INTEGER", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedReason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    FirstLoginNotifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorCredentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Operators_Email",
                table: "Operators",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Operators_Username",
                table: "Operators",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatorCredentials_OperatorId",
                table: "OperatorCredentials",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorCredentials_WebAuthnCredentialId",
                table: "OperatorCredentials",
                column: "WebAuthnCredentialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorCredentials");

            migrationBuilder.DropIndex(
                name: "IX_Operators_Email",
                table: "Operators");

            migrationBuilder.DropIndex(
                name: "IX_Operators_Username",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "ApprovalFirstLoginNotifiedAtUtc",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "PasswordUpdatedAtUtc",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Operators");
        }
    }
}
