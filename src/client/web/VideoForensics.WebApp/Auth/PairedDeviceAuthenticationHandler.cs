using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Auth
{
    public static class PairedDeviceAuthenticationDefaults
    {
        public const string SchemeName = "PairedDevice";
    }

    /// <summary>
    /// Validates the "Authorization: Bearer &lt;token&gt;" header issued by the pairing/assertion
    /// endpoints (plan §5.1/§5.10). Every request re-checks the paired device's CURRENT revocation
    /// status against the database - not just the token's own signature/expiry - so a revoked
    /// device is locked out within one request round-trip (§5.4), not only once its token expires.
    /// </summary>
    public class PairedDeviceAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly ISessionTokenService _tokenService;
        private readonly IPairedDeviceRepository _pairedDeviceRepository;
        private readonly INetworkTierResolver _tierResolver;

        public PairedDeviceAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISessionTokenService tokenService,
            IPairedDeviceRepository pairedDeviceRepository,
            INetworkTierResolver tierResolver)
            : base(options, logger, encoder)
        {
            _tokenService = tokenService;
            _pairedDeviceRepository = pairedDeviceRepository;
            _tierResolver = tierResolver;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                return AuthenticateResult.NoResult();
            }

            var headerValue = authHeader.ToString();
            if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.NoResult();
            }

            var token = headerValue["Bearer ".Length..].Trim();
            var principal = _tokenService.Validate(token);
            if (principal == null)
            {
                return AuthenticateResult.Fail("Invalid or expired session token.");
            }

            // Re-checked on EVERY request, deliberately not cached in the token itself - see the
            // class doc comment. A device revoked mid-session must be rejected here immediately.
            var device = await _pairedDeviceRepository.GetAsync(principal.PairedDeviceId, Context.RequestAborted);
            if (device == null || !device.IsActive)
            {
                return AuthenticateResult.Fail("This device's pairing has been revoked.");
            }

            var tier = _tierResolver.ResolveTier(Context);
            var claims = new[]
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
