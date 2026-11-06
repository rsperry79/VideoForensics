using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRingAccountMonitoringStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RingAccountMonitoringStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderLocationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AddressComplete = table.Column<bool>(type: "INTEGER", nullable: true),
                    ContactsComplete = table.Column<bool>(type: "INTEGER", nullable: true),
                    CodewordComplete = table.Column<bool>(type: "INTEGER", nullable: true),
                    DispatchSetupComplete = table.Column<bool>(type: "INTEGER", nullable: true),
                    UserChangedAddress = table.Column<bool>(type: "INTEGER", nullable: true),
                    AlarmSignalSent = table.Column<bool>(type: "INTEGER", nullable: true),
                    ProfessionallyMonitored = table.Column<bool>(type: "INTEGER", nullable: true),
                    UserAcceptDispatch = table.Column<bool>(type: "INTEGER", nullable: true),
                    VrRequired = table.Column<bool>(type: "INTEGER", nullable: true),
                    VrUserOptIn = table.Column<bool>(type: "INTEGER", nullable: true),
                    CmsMonitoringType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SuspectedBusiness = table.Column<bool>(type: "INTEGER", nullable: true),
                    AccountStateReason = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AccountUuid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AccountState = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ExternalServiceConfigType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EligibleForDispatch = table.Column<bool>(type: "INTEGER", nullable: true),
                    SosOnly = table.Column<bool>(type: "INTEGER", nullable: true),
                    InstallationDate = table.Column<long>(type: "INTEGER", nullable: true),
                    DeliverSignals = table.Column<bool>(type: "INTEGER", nullable: true),
                    PlanProvider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SosOnlyFlagOrFalse = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccountMonitoringStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingAccountMonitoringPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RingAccountMonitoringStatusId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Plan = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingAccountMonitoringPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RingAccountMonitoringPlans_RingAccountMonitoringStatuses_RingAccountMonitoringStatusId",
                        column: x => x.RingAccountMonitoringStatusId,
                        principalTable: "RingAccountMonitoringStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RingAccountMonitoringPlans_RingAccountMonitoringStatusId",
                table: "RingAccountMonitoringPlans",
                column: "RingAccountMonitoringStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_RingAccountMonitoringStatuses_ProviderLocationId",
                table: "RingAccountMonitoringStatuses",
                column: "ProviderLocationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RingAccountMonitoringPlans");

            migrationBuilder.DropTable(
                name: "RingAccountMonitoringStatuses");
        }
    }
}
