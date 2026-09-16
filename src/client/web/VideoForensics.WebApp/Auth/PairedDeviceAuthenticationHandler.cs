using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Auth
{
    public static class PairedDeviceAuthenticationDefaults
    {
        public const string SchemeName = "PairedDevice";
    }

    /// <summary>
    /// Validates the "Authorization: Bearer &lt;token&gt;" header issued by the pairing/assertion
    /// endpoints (plan §5.1/§5.10). Accepts two credential types:
    /// - Session tokens issued by browser-based pairing (WebAuthn)
    /// - Long-lived fallback API keys issued by device-code-based pairing (for headless CLI)
    /// Every request re-checks the paired device's CURRENT revocation status against the
    /// database - not just the token's own signature/expiry - so a revoked device is locked out
    /// within one request round-trip (§5.4), not only once its token expires.
    /// </summary>
    public class PairedDeviceAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly ISessionTokenService _tokenService;
        private readonly IPairedDeviceRepository _pairedDeviceRepository;
        private readonly INetworkTierResolver _tierResolver;
        private readonly IOperatorRepository _operatorRepository;

        public PairedDeviceAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISessionTokenService tokenService,
            IPairedDeviceRepository pairedDeviceRepository,
            INetworkTierResolver tierResolver,
            IOperatorRepository operatorRepository)
            : base(options, logger, encoder)
        {
            _tokenService = tokenService;
            _pairedDeviceRepository = pairedDeviceRepository;
            _tierResolver = tierResolver;
            _operatorRepository = operatorRepository;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? token = ExtractToken();
            if (token is null)
            {
                return AuthenticateResult.NoResult();
            }

            SessionPrincipal? principal = _tokenService.Validate(token);
            if (principal != null)
            {
                // Session-token path (browser-based pairing with WebAuthn)
                // Re-checked on EVERY request, deliberately not cached in the token itself - see the
                // class doc comment. A device revoked mid-session must be rejected here immediately.
                PairedDevice? device = await _pairedDeviceRepository.GetAsync(principal.PairedDeviceId, Context.RequestAborted);
                if (device == null || !device.IsActive)
                {
                    return AuthenticateResult.Fail("Invalid or expired credential.");
                }

                // Verify operator is approved and active
                Operator? op = await _operatorRepository.GetAsync(device.OperatorId, Context.RequestAborted);
                if (op == null || !op.IsApproved || !op.Active)
                {
                    return AuthenticateResult.Fail("Invalid or expired credential.");
                }

                NetworkTier tier = _tierResolver.ResolveTier(Context);
                Claim[] claims = new[]
                {
                    new Claim(VideoForensicsClaimTypes.OperatorId, principal.OperatorId.ToString()),
                    new Claim(VideoForensicsClaimTypes.PairedDeviceId, principal.PairedDeviceId.ToString()),
                    new Claim(VideoForensicsClaimTypes.Role, principal.Role.ToString()),
                    new Claim(VideoForensicsClaimTypes.NetworkTier, tier.ToString())
                };

                var identity = new ClaimsIdentity(claims, PairedDeviceAuthenticationDefaults.SchemeName);
                var claimsPrincipal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(claimsPrincipal, PairedDeviceAuthenticationDefaults.SchemeName);
                return AuthenticateResult.Success(ticket);
            }

            // Fallback-API-key path (device-code-based pairing for headless CLI)
            // Hash the raw token and look it up in the database.
            string apiKeyHash = HashApiKey(token);
            PairedDevice? fallbackDevice = await _pairedDeviceRepository.GetByFallbackApiKeyHashAsync(apiKeyHash, Context.RequestAborted);
            if (fallbackDevice == null || !fallbackDevice.IsActive)
            {
                return AuthenticateResult.Fail("Invalid or expired credential.");
            }

            NetworkTier fallbackTier = _tierResolver.ResolveTier(Context);
            Claim[] fallbackClaims = new[]
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, fallbackDevice.OperatorId.ToString()),
                new Claim(VideoForensicsClaimTypes.PairedDeviceId, fallbackDevice.Id.ToString()),
                new Claim(VideoForensicsClaimTypes.Role, fallbackDevice.Role.ToString()),
                new Claim(VideoForensicsClaimTypes.NetworkTier, fallbackTier.ToString())
            };

            var fallbackIdentity = new ClaimsIdentity(fallbackClaims, PairedDeviceAuthenticationDefaults.SchemeName);
            var fallbackClaimsPrincipal = new ClaimsPrincipal(fallbackIdentity);
            var fallbackTicket = new AuthenticationTicket(fallbackClaimsPrincipal, PairedDeviceAuthenticationDefaults.SchemeName);
            return AuthenticateResult.Success(fallbackTicket);
        }

        /// <summary>
        /// Computes the SHA-256 hash of the raw API key and returns it as a lowercase hex string.
        /// </summary>
        private static string HashApiKey(string apiKey)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
            return Convert.ToHexStringLower(hash);
        }

        /// <summary>
        /// Normal requests carry the bearer token in the Authorization header. SignalR's WebSocket
        /// transport is the one exception - browsers/most WebSocket clients cannot set custom
        /// headers on the upgrade request, so the token has to travel as an "access_token" query
        /// string parameter instead (the same accommodation ASP.NET Core's own JWT bearer handler
        /// makes for SignalR). Restricted to "/hubs/" paths specifically, so a token never has a
        /// reason to appear in a query string - and therefore in server access logs - anywhere else.
        /// </summary>
        private string? ExtractToken()
        {
            if (Request.Headers.TryGetValue("Authorization", out StringValues authHeader))
            {
                string headerValue = authHeader.ToString();
                if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return headerValue["Bearer ".Length..].Trim();
                }
            }

            return Request.Path.StartsWithSegments("/hubs") && Request.Query.TryGetValue("access_token", out StringValues queryToken)
                ? queryToken.ToString()
                : null;
        }
    }

    /// <summary>Claim type names used by the PairedDevice authentication scheme.</summary>
    public static class VideoForensicsClaimTypes
    {
        public const string OperatorId = "vf:operator_id";
        public const string PairedDeviceId = "vf:paired_device_id";
        public const string Role = "vf:role";
        public const string NetworkTier = "vf:network_tier";
    }
}
