using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Admin endpoints for managing two-factor authentication policy settings: per-role requirements
    /// and per-operator overrides.
    /// All routes require SuperAdmin+Local authorization (plan §5.10/§5.12).
    /// </summary>
    public static class TwoFactorPolicyEndpoints
    {
        public static void MapTwoFactorPolicyEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/two-factor-policy").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            _ = group.MapGet("/roles", GetAllRolesAsync).WithName("GetTwoFactorRoleRequirements");
            _ = group.MapPut("/roles/{role}", UpdateRoleRequirementAsync).WithName("UpdateTwoFactorRoleRequirement").AddEndpointFilter<StepUpEndpointFilter>();
            _ = group.MapPut("/operators/{id}/override", UpdateOperatorOverrideAsync).WithName("UpdateOperatorTwoFactorOverride").AddEndpointFilter<StepUpEndpointFilter>();
        }

        private static async Task<IResult> GetAllRolesAsync(
            ITwoFactorRoleRequirementRepository repository,
            CancellationToken ct)
        {
            var requirements = await repository.GetAllAsync(ct);
            var dtos = requirements.Select(r => r.ToDto()).ToList();
            return Results.Ok(dtos);
        }

        private static async Task<IResult> UpdateRoleRequirementAsync(
            OperatorRole role,
            UpdateTwoFactorRoleRequirementRequest request,
            ITwoFactorRoleRequirementRepository repository,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            var requirement = new TwoFactorRoleRequirement
            {
                Id = Guid.NewGuid(),
                Role = role,
                RequireTwoFactor = request.RequireTwoFactor,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId) is { Value: not null and not "" } claim
                    && Guid.TryParse(claim.Value, out Guid operatorId) ? operatorId : null
            };

            await repository.UpsertAsync(requirement, ct);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            string details = $"Role: {role}, RequireTwoFactor: {request.RequireTwoFactor}";
            await auditLog.LogAsync(SecurityAuditEventTypes.TwoFactorPolicyUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), details, isUrgent: true, ct);

            return Results.Ok(requirement.ToDto());
        }

        private static async Task<IResult> UpdateOperatorOverrideAsync(
            Guid id,
            UpdateOperatorTwoFactorOverrideRequest request,
            IOperatorRepository operators,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            Operator? op = await operators.GetAsync(id, ct);
            if (op == null)
            {
                return Results.NotFound(new { error = "Operator not found" });
            }

            await operators.SetTwoFactorRequirementOverrideAsync(id, request.Override, ct);

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            string details = $"Operator: {op.DisplayName}, Override: {request.Override}";
            await auditLog.LogAsync(SecurityAuditEventTypes.TwoFactorPolicyUpdated,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), details, isUrgent: true, ct);

            return Results.Ok(new { operatorId = op.Id, @override = request.Override.ToString() });
        }
    }
}
