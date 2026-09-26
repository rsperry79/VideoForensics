using Microsoft.AspNetCore.Identity;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// First-run setup: lets the installing user create their own initial SuperAdmin account with a
    /// username and password of their choosing, instead of relying on the fixed
    /// admin/ChangeMe123! seed (see VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync).
    ///
    /// Gated the same way PairingEndpoints.initiate gates its bootstrap case: only actionable while
    /// IOperatorRepository.IsEmptyAsync() is true, becoming Forbid() once any operator exists - this
    /// reuses the exact same "first run" signal as the seeding block and AuthGate.razor rather than
    /// introducing a second source of truth (e.g. a separate "SetupComplete" flag).
    /// </summary>
    public static class SetupEndpoints
    {
        public static void MapSetupEndpoints(this WebApplication app)
        {
            _ = app.MapPost("/api/v1/setup/create-admin", CreateAdminAsync)
                .RequireRateLimiting("auth");
        }

        /// <summary>Extracted from the MapPost lambda so it's directly unit-testable (see
        /// SetupEndpointsTests.cs) without spinning up a full WebApplicationFactory host.</summary>
        public static async Task<IResult> CreateAdminAsync(
            CreateSetupAdminRequest request,
            IOperatorRepository operators,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            if (!await operators.IsEmptyAsync(ct))
            {
                return Results.Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return Results.BadRequest(new { error = "Username is required." });
            }

            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 12)
            {
                return Results.BadRequest(new { error = "Password must be at least 12 characters." });
            }

            var passwordHasher = new PasswordHasher<Operator>();
            var admin = new Operator
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                DisplayName = request.Username,
                FirstName = request.Username,
                LastName = "Administrator",
                Email = $"{request.Username}@localhost.invalid",
                Role = OperatorRole.SuperAdmin,
                IsApproved = true,
                Active = true,
                MustChangePassword = false,
                CreatedAtUtc = DateTime.UtcNow,
                PasswordUpdatedAtUtc = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid()
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, request.Password);

            Operator created = await operators.AddAsync(admin, ct);

            await auditLog.LogAsync(SecurityAuditEventTypes.SetupAdminCreated, created.Id, null,
                tierResolver.ResolveClientIp(context), $"First-run setup created SuperAdmin '{created.Username}'", isUrgent: true, ct);

            return Results.Ok(new { operatorId = created.Id });
        }
    }

    public record CreateSetupAdminRequest(string Username, string Password);
}
