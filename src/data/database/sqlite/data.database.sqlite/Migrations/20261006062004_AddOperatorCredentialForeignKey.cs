using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorCredentialForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperatorCredentials_WebAuthnCredentialId",
                table: "OperatorCredentials");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorCredentials_WebAuthnCredentialId",
                table: "OperatorCredentials",
                column: "WebAuthnCredentialId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperatorCredentials_WebAuthnCredentialId",
                table: "OperatorCredentials");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorCredentials_WebAuthnCredentialId",
                table: "OperatorCredentials",
                column: "WebAuthnCredentialId");
        }
    }
}
