using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Auth
{
    /// <summary>
    /// Requires a valid "X-StepUp-Token" header (issued by POST /api/auth/webauthn/stepup-complete)
    /// for the CURRENT session's device, in addition to the normal session - see
    /// IStepUpAuthService's doc comment for why this is a separate check from session auth (plan §5.7).
    /// </summary>
    public class StepUpEndpointFilter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var httpContext = context.HttpContext;
            var deviceIdClaim = httpContext.User.FindFirst(VideoForensicsClaimTypes.PairedDeviceId)?.Value;
            if (!Guid.TryParse(deviceIdClaim, out var pairedDeviceId))
            {
                return Results.Unauthorized();
            }

            if (!httpContext.Request.Headers.TryGetValue("X-StepUp-Token", out var tokenHeader))
            {
                return Results.Json(new { error = "This action requires step-up re-authentication (X-StepUp-Token header missing)." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var stepUpAuth = httpContext.RequestServices.GetRequiredService<IStepUpAuthService>();
            if (!stepUpAuth.Validate(tokenHeader.ToString(), pairedDeviceId))
            {
                return Results.Json(new { error = "Step-up token invalid or expired - re-authenticate and retry." }, statusCode: StatusCodes.Status403Forbidden);
            }

            return await next(context);
        }
    }
}
