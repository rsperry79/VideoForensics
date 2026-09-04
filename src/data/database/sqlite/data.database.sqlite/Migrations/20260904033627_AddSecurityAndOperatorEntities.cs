using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityAndOperatorEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Operators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PairedDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    WebAuthnCredentialId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    WebAuthnPublicKey = table.Column<byte[]>(type: "BLOB", nullable: true),
                    WebAuthnSignCount = table.Column<uint>(type: "INTEGER", nullable: false),
                    FallbackApiKeyHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PinnedCertificateFingerprint = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PairedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSeenIp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LastSeenTier = table.Column<int>(type: "INTEGER", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedReason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PairedDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PairedDeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceIp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    IsUrgent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PairedDevices_FallbackApiKeyHash",
                table: "PairedDevices",
                column: "FallbackApiKeyHash");

            migrationBuilder.CreateIndex(
                name: "IX_PairedDevices_OperatorId",
                table: "PairedDevices",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_PairedDevices_WebAuthnCredentialId",
                table: "PairedDevices",
                column: "WebAuthnCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditLogEntries_OperatorId",
                table: "SecurityAuditLogEntries",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditLogEntries_TimestampUtc",
                table: "SecurityAuditLogEntries",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Operators");

            migrationBuilder.DropTable(
                name: "PairedDevices");

            migrationBuilder.DropTable(
                name: "SecurityAuditLogEntries");
        }
    }
}
