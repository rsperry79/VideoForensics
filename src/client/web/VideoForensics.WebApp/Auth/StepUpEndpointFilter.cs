using Microsoft.Extensions.Primitives;

using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Auth
{
    /// <summary>
    /// Requires a valid "X-StepUp-Token" header (issued by POST /api/auth/webauthn/stepup-complete or /api/v1/auth/stepup/password)
    /// for the CURRENT session's device/credential, in addition to the normal session - see
    /// IStepUpAuthService's doc comment for why this is a separate check from session auth (plan §5.7).
    ///
    /// For ServiceDevice credentials (PairedDevice-based), validates against the PairedDeviceId claim.
    /// For Password/OperatorPasskey credentials (no device), validates against the OperatorId claim instead.
    /// </summary>
    public class StepUpEndpointFilter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            HttpContext httpContext = context.HttpContext;

            // Try PairedDeviceId first (ServiceDevice or OperatorPasskey)
            string? deviceIdClaim = httpContext.User.FindFirst(VideoForensicsClaimTypes.PairedDeviceId)?.Value;
            Guid? subjectId = null;

            if (Guid.TryParse(deviceIdClaim, out Guid pairedDeviceId))
            {
                subjectId = pairedDeviceId;
            }
            else
            {
                // Fall back to OperatorId for Password sessions (no device/credential claim)
                string? operatorIdClaim = httpContext.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                if (Guid.TryParse(operatorIdClaim, out Guid operatorId))
                {
                    subjectId = operatorId;
                }
            }

            // If neither claim parses, deny
            if (subjectId == null)
            {
                return Results.Unauthorized();
            }

            if (!httpContext.Request.Headers.TryGetValue("X-StepUp-Token", out StringValues tokenHeader))
            {
                return Results.Json(new { error = "This action requires step-up re-authentication (X-StepUp-Token header missing)." }, statusCode: StatusCodes.Status403Forbidden);
            }

            IStepUpAuthService stepUpAuth = httpContext.RequestServices.GetRequiredService<IStepUpAuthService>();
            return !stepUpAuth.Validate(tokenHeader.ToString(), subjectId.Value)
                ? Results.Json(new { error = "Step-up token invalid or expired - re-authenticate and retry." }, statusCode: StatusCodes.Status403Forbidden)
                : await next(context);
        }
    }
}
