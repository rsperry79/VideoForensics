using VideoForensics.Api.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Provider authentication endpoints (plan §6/M7): login and two-factor authentication flows.
    ///
    /// SECURITY NOTE, stated explicitly rather than silently glossed over: these endpoints are
    /// PRE-AUTHENTICATION (a caller is not yet authenticated when logging into a provider account).
    /// They are NOT gated by RequireAuthorization - this is a deliberate, justified exception
    /// from the general principle of authentication on write paths, not an oversight.
    /// The endpoints are rate-limited by IP/network-tier per rate-limiting policy to defend against
    /// brute-force attacks, and credentials are never persisted in HTTP request logs.
    ///
    /// The login flow handles two-factor authentication by storing authentication attempts
    /// in an in-memory cache (IAuthAttemptCache). When AuthenticateAsync raises
    /// TwoFactorAuthenticationRequiredException, the endpoint returns RequiresTwoFactor = true
    /// with an AuthAttemptId; the client then calls the two-factor endpoint with that ID
    /// and the user-supplied code. The server retrieves the cached credentials, calls
    /// AuthenticateWithTwoFactorAsync with the code callback, and returns the result.
    /// Attempts expire after 5 minutes and are consumed (removed) on use or expiry.
    /// </summary>
    public static class AuthEndpoints
    {
        public static void MapAuthEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/auth");

            _ = group.MapPost("/login", LoginAsync)
                .RequireRateLimiting("auth")
                .WithSummary("Authenticate with provider credentials")
                .WithDescription("Initiates authentication with the configured provider (Ring, Wyze, etc.) using username/password. " +
                    "If two-factor authentication is required, returns RequiresTwoFactor=true with an AuthAttemptId for the follow-up.");

            _ = group.MapPost("/login/two-factor", TwoFactorAsync)
                .RequireRateLimiting("auth")
                .WithSummary("Complete two-factor authentication")
                .WithDescription("Completes authentication by submitting a two-factor code received after an initial login attempt. " +
                    "The AuthAttemptId from the initial login response ties this request back to the original authentication attempt.");

            _ = group.MapGet("/providers", (IMultiProviderAuthService multi, CancellationToken ct) => multi.GetAvailableProvidersAsync(ct))
                .WithSummary("List provider names available for account linking")
                .WithDescription("Returns the names of all providers this server can authenticate against (e.g. Ring, Wyze, Uniview), for populating a provider picker in the Add Account UI.");
        }

        private static async Task<IResult> LoginAsync(
            LoginRequestDto request,
            IProviderAuthService authService,
            IAuthAttemptCache attemptCache,
            IMultiProviderAuthService multiProviderAuthService,
            CancellationToken ct)
        {
            IProviderAuthService resolvedAuthService = string.IsNullOrEmpty(request.ProviderName)
                ? authService
                : multiProviderAuthService.GetService(request.ProviderName);

            try
            {
                // Attempt initial authentication (without 2FA callback)
                AuthResult result = await resolvedAuthService.AuthenticateAsync(
                    request.Username,
                    request.Password,
                    ct);

                return Results.Ok(MapAuthResult(result));
            }
            catch (VideoForensics.Providers.Ring.Exceptions.TwoFactorAuthenticationRequiredException)
            {
                // 2FA is required: store credentials for the follow-up call and return a pending state
                Guid attemptId = attemptCache.StoreAttempt(request.ProviderName, request.Username, request.Password);

                return Results.Ok(new AuthResultDto(
                    Success: false,
                    ErrorMessage: null,
                    AuthToken: null,
                    ExpiresAt: null,
                    ProviderAccountId: null,
                    RequiresTwoFactor: true,
                    AuthAttemptId: attemptId
                ));
            }
            catch (Exception ex)
            {
                // Any other exception is an unexpected error, not a 2FA-required scenario
                return Results.BadRequest(new AuthResultDto(
                    Success: false,
                    ErrorMessage: $"Authentication error: {ex.Message}"
                ));
            }
        }

        private static async Task<IResult> TwoFactorAsync(
            TwoFactorRequestDto request,
            IProviderAuthService authService,
            IAuthAttemptCache attemptCache,
            IMultiProviderAuthService multiProviderAuthService,
            CancellationToken ct)
        {
            // Retrieve stored credentials for this attempt
            (string? ProviderName, string? Username, string? Password)? attempt = attemptCache.GetAndRemoveAttempt(request.AuthAttemptId);
            if (attempt == null)
            {
                return Results.BadRequest(new AuthResultDto(
                    Success: false,
                    ErrorMessage: "Two-factor attempt expired or invalid. Please log in again."
                ));
            }

            string username = attempt.Value.Username!;
            string password = attempt.Value.Password!;

            IProviderAuthService resolvedAuthService = string.IsNullOrEmpty(attempt.Value.ProviderName)
                ? authService
                : multiProviderAuthService.GetService(attempt.Value.ProviderName);

            try
            {
                // Create a callback that returns the user's 2FA code
                Task<string> codeProvider()
                {
                    return Task.FromResult(request.Code);
                }

                // Attempt authentication with 2FA code
                AuthResult result = await resolvedAuthService.AuthenticateWithTwoFactorAsync(
                    username,
                    password,
codeProvider,
                    ct);

                return Results.Ok(MapAuthResult(result));
            }
            catch (VideoForensics.Providers.Ring.Exceptions.TwoFactorAuthenticationIncorrectException)
            {
                return Results.BadRequest(new AuthResultDto(
                    Success: false,
                    ErrorMessage: "Two-factor authentication code was incorrect. Please try again."
                ));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new AuthResultDto(
                    Success: false,
                    ErrorMessage: $"Two-factor authentication error: {ex.Message}"
                ));
            }
        }

        /// <summary>
        /// Maps IProviderAuthService.AuthResult to API contract AuthResultDto.
        /// </summary>
        private static AuthResultDto MapAuthResult(AuthResult result)
        {
            return new AuthResultDto(
                Success: result.Success,
                ErrorMessage: result.ErrorMessage,
                AuthToken: result.AuthToken,
                ExpiresAt: result.ExpiresAt,
                ProviderAccountId: result.ProviderAccountId,
                RequiresTwoFactor: false,
                AuthAttemptId: null
            );
        }
    }
}
