using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>Operator notice reads and dismissals under <c>/api/v1/notices</c>. The operator and role come from the bearer token.</summary>
    public static class NoticeEndpoints
    {
        public static void MapNoticeEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/notices").RequireAuthorization();

            _ = group.MapGet("/", async (
                bool? includeDismissed,
                HttpContext context,
                INoticeRepository notices,
                CancellationToken ct) =>
            {
                if (!TryGetOperator(context, out Guid operatorId, out bool isAdmin))
                {
                    return Results.Unauthorized();
                }

                IReadOnlyList<NoticeDto> rows = await notices.ListForOperatorAsync(operatorId, isAdmin, includeDismissed ?? false, ct);
                return Results.Ok(rows);
            })
                .WithSummary("List notices for the signed-in operator")
                .WithDescription("Returns the notices visible to the operator. Set includeDismissed=true to include dismissed ones.");

            _ = group.MapGet("/undismissed-count", async (
                HttpContext context,
                INoticeRepository notices,
                CancellationToken ct) =>
            {
                if (!TryGetOperator(context, out Guid operatorId, out bool isAdmin))
                {
                    return Results.Unauthorized();
                }

                int count = await notices.CountUndismissedForOperatorAsync(operatorId, isAdmin, ct);
                return Results.Ok(new UndismissedNoticeCountDto(count));
            })
                .WithSummary("Count undismissed notices for the signed-in operator");

            _ = group.MapPost("/{id:guid}/dismiss", async (
                Guid id,
                HttpContext context,
                INoticeDismissalRepository dismissals,
                CancellationToken ct) =>
            {
                if (!TryGetOperator(context, out Guid operatorId, out _))
                {
                    return Results.Unauthorized();
                }

                await dismissals.DismissAsync(id, operatorId, ct);
                return Results.NoContent();
            })
                .WithSummary("Dismiss one notice for the signed-in operator");

            _ = group.MapPost("/dismiss-all", async (
                Guid[] ids,
                HttpContext context,
                INoticeDismissalRepository dismissals,
                CancellationToken ct) =>
            {
                if (!TryGetOperator(context, out Guid operatorId, out _))
                {
                    return Results.Unauthorized();
                }

                await dismissals.DismissAllAsync(ids, operatorId, ct);
                return Results.NoContent();
            })
                .WithSummary("Dismiss several notices for the signed-in operator");
        }

        private static bool TryGetOperator(HttpContext context, out Guid operatorId, out bool isAdmin)
        {
            isAdmin = false;
            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (!Guid.TryParse(operatorIdClaim, out operatorId))
            {
                return false;
            }

            string? roleClaim = context.User.FindFirst(VideoForensicsClaimTypes.Role)?.Value;
            isAdmin = Enum.TryParse(roleClaim, out OperatorRoleDto role) && role >= OperatorRoleDto.Admin;
            return true;
        }
    }
}
