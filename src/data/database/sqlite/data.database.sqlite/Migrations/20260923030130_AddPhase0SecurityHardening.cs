using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase0SecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttemptCount",
                table: "Operators",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimarySuperAdmin",
                table: "Operators",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedOutUntilUtc",
                table: "Operators",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TwoFactorRequirementOverride",
                table: "Operators",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BannedIpRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CidrRange = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByOperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannedIpRanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LockoutPolicySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaxFailedAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LockoutDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    BlockedCountryCodes = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FailClosedOnLookupError = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedByOperatorId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LockoutPolicySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TwoFactorRoleRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    RequireTwoFactor = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedByOperatorId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorRoleRequirements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BannedIpRanges_CidrRange",
                table: "BannedIpRanges",
                column: "CidrRange");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorRoleRequirements_Role",
                table: "TwoFactorRoleRequirements",
                column: "Role",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BannedIpRanges");

            migrationBuilder.DropTable(
                name: "LockoutPolicySettings");

            migrationBuilder.DropTable(
                name: "TwoFactorRoleRequirements");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttemptCount",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "IsPrimarySuperAdmin",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "LockedOutUntilUtc",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "TwoFactorRequirementOverride",
                table: "Operators");
        }
    }
}
