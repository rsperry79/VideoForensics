using Microsoft.Extensions.Primitives;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Infrastructure;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// The configured network access tier (plan §5.2) - Local by default, each wider tier an
    /// explicit opt-in. Gated SuperAdmin (Local for password sign-in; passkey from any tier) like every other infrastructure-exposure screen
    /// (§5.10). Only WIDENING (Local→Network, Local→Internet, Network→Internet) requires step-up
    /// re-authentication (§5.7's own "widening the network tier" example) - narrowing back toward
    /// Local only reduces exposure, so it's allowed without a fresh passkey assertion, the same
    /// asymmetry RemoteAccessEndpoints already applies to tunnel start vs. stop. Implemented inline
    /// here rather than via the reusable StepUpEndpointFilter, since that filter is unconditional
    /// and this needs to depend on the requested value, not just the route.
    /// </summary>
    public static class NetworkSettingsEndpoints
    {
        public static void MapNetworkSettingsEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/network-settings").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey);

            _ = group.MapGet("/", (IForensicsConfiguration config) => Results.Ok(new
            {
                configuredTier = config.ConfiguredNetworkTier.ToString(),
                // Kestrel's actual bind addresses are fixed at process startup (Program.cs reads
                // this same setting before the host is built) - a change here only takes effect
                // after the next restart, which the UI must say plainly rather than implying it's
                // live.
                requiresRestartToTakeEffect = true
            }));

            _ = group.MapPost("/", SetNetworkTierAsync);

            _ = group.MapGet("/internet-url", (IForensicsConfiguration config) => Results.Ok(new
            {
                internetServerUrl = config.InternetServerUrl
            }));

            _ = group.MapPost("/internet-url", async (
                SetInternetServerUrlRequest request,
                IForensicsConfiguration config,
                IForensicsConfigurationService configService,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                string? oldValue = config.InternetServerUrl;

                // Setting InternetServerUrl is not a "widening" network-tier change in the sense that
                // it does not change what Kestrel binds to - it only affects where paired clients know
                // to find the server over the Internet. Therefore, it does not require step-up re-authentication,
                // only SuperAdmin authorization inherited from the group.
                config.InternetServerUrl = request.InternetServerUrl;
                await configService.SaveConfigurationAsync(config, ct);

                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                await auditLog.LogAsync(SecurityAuditEventTypes.NetworkTierChanged,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    null, tierResolver.ResolveClientIp(context), $"InternetServerUrl changed from '{oldValue}' to '{request.InternetServerUrl}'", isUrgent: true, ct);

                return Results.Ok(new { internetServerUrl = config.InternetServerUrl });
            });

            _ = group.MapGet("/internal-url", (IForensicsConfiguration config, IConfiguration configuration) => Results.Ok(new
            {
                // A blank saved value means "not set", so it gets the machine's DNS name like a never-saved one.
                internalServerUrl = string.IsNullOrWhiteSpace(config.InternalServerUrl) ? ServerAddressResolver.ResolveDefaultInternalUrl(configuration) : config.InternalServerUrl
            }));

            _ = group.MapPost("/internal-url", async (
                SetInternalServerUrlRequest request,
                IForensicsConfiguration config,
                IForensicsConfigurationService configService,
                IConfiguration configuration,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                // Blank returns the setting to the machine's DNS name.
                string? url = string.IsNullOrWhiteSpace(request.InternalServerUrl) ? null : request.InternalServerUrl.Trim();
                if (url is not null)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host))
                    {
                        return Results.BadRequest(new { error = "Enter a full http:// or https:// address." });
                    }

                    // An unresolvable name would give paired devices an address they cannot reach, so it is rejected here.
                    if (!await ServerAddressResolver.IsResolvableAsync(uri.Host, ct))
                    {
                        return Results.BadRequest(new { error = $"'{uri.Host}' does not resolve. Check the name, or use this machine's DNS name." });
                    }
                }

                string? oldValue = config.InternalServerUrl;
                config.InternalServerUrl = url;
                await configService.SaveConfigurationAsync(config, ct);

                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                await auditLog.LogAsync(SecurityAuditEventTypes.InternalServerUrlChanged,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    null, tierResolver.ResolveClientIp(context), $"InternalServerUrl changed from '{oldValue}' to '{url}'", isUrgent: true, ct);

                return Results.Ok(new { internalServerUrl = config.InternalServerUrl ?? ServerAddressResolver.ResolveDefaultInternalUrl(configuration) });
            });

            _ = group.MapGet("/server-ports", (IForensicsConfiguration config, IConfiguration configuration) => Results.Ok(new
            {
                httpPort = config.ServerHttpPort > 0 ? config.ServerHttpPort : ServerAddressResolver.ResolveListenEndpoints(configuration).FirstOrDefault(e => e.Scheme == "http")?.Port,
                httpsPort = config.ServerHttpsPort > 0 ? config.ServerHttpsPort : ServerAddressResolver.ResolveListenEndpoints(configuration).FirstOrDefault(e => e.Scheme == "https")?.Port ?? ServerAddressResolver.DefaultHttpsPort
            }));

            _ = group.MapPost("/server-ports", async (
                SetServerPortsRequest request,
                IForensicsConfiguration config,
                IForensicsConfigurationService configService,
                IConfiguration configuration,
                IFirewallRuleManager firewallRuleManager,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                ILetsEncryptCertificateStore certificateStore,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (!IsValidPort(request.HttpPort) || !IsValidPort(request.HttpsPort))
                {
                    return Results.BadRequest(new { error = "Enter a port from 1 to 65535 for both HTTP and HTTPS." });
                }

                if (request.HttpPort == request.HttpsPort)
                {
                    return Results.BadRequest(new { error = "The HTTP and HTTPS ports must be different." });
                }

                string? error = PortError("HTTP", request.HttpPort, "http", configuration) ?? PortError("HTTPS", request.HttpsPort, "https", configuration);
                if (error is not null)
                {
                    return Results.BadRequest(new { error });
                }

                // Only a change to the HTTPS port needs a certificate. Saving the HTTP port alone keeps the current HTTPS port.
                int currentHttpsPort = config.ServerHttpsPort > 0 ? config.ServerHttpsPort : ServerAddressResolver.DefaultHttpsPort;
                if (request.HttpsPort != currentHttpsPort && !await HasValidCertificateAsync(certificateStore, ct))
                {
                    return Results.BadRequest(new { error = "HTTPS needs a valid Let's Encrypt certificate. Request one first." });
                }

                string? oldPorts = $"HTTP {config.ServerHttpPort}, HTTPS {config.ServerHttpsPort}";
                config.ServerHttpPort = request.HttpPort;
                config.ServerHttpsPort = request.HttpsPort;
                await configService.SaveConfigurationAsync(config, ct);

                // The firewall rule follows the HTTP port, the same way the tier widening does. Local tier needs no rule.
                if (config.ConfiguredNetworkTier != NetworkTier.Local)
                {
                    await firewallRuleManager.RemoveRuleAsync("VideoForensics", ct);
                    await firewallRuleManager.EnsureRuleExistsAsync("VideoForensics", request.HttpPort, ct);
                }

                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                await auditLog.LogAsync(SecurityAuditEventTypes.ServerPortChanged,
                    Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                    null, tierResolver.ResolveClientIp(context), $"Server ports changed from {oldPorts} to HTTP {request.HttpPort}, HTTPS {request.HttpsPort}", isUrgent: true, ct);

                return Results.Ok(new { httpPort = request.HttpPort, httpsPort = request.HttpsPort, requiresRestartToTakeEffect = true });
            });
        }

        /// <summary>
        /// Extracted from the MapPost lambda so it's directly unit-testable (see
        /// NetworkSettingsEndpointsTests.cs) without spinning up a full WebApplicationFactory host.
        /// </summary>
        public static async Task<IResult> SetNetworkTierAsync(
            SetNetworkTierRequest request,
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            IStepUpAuthService stepUpAuth,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            IFirewallRuleManager firewallRuleManager,
            IConfiguration configuration,
            HttpContext context,
            CancellationToken ct)
        {
            NetworkTier currentTier = config.ConfiguredNetworkTier;
            bool isWidening = request.Tier > currentTier;

            if (isWidening)
            {
                string? deviceIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.PairedDeviceId)?.Value;
                if (!Guid.TryParse(deviceIdClaim, out Guid pairedDeviceId))
                {
                    return Results.Unauthorized();
                }

                if (!context.Request.Headers.TryGetValue("X-StepUp-Token", out StringValues stepUpToken)
                    || !stepUpAuth.Validate(stepUpToken.ToString(), pairedDeviceId))
                {
                    return Results.Json(
                        new { error = "Widening the network tier requires step-up re-authentication (X-StepUp-Token header missing or invalid)." },
                        statusCode: StatusCodes.Status403Forbidden);
                }
            }

            config.ConfiguredNetworkTier = request.Tier;
            await configService.SaveConfigurationAsync(config, ct);

            // Kestrel's own bind (Program.cs) only controls whether the socket listens on every interface -
            // it doesn't open the Windows Firewall to let inbound traffic actually reach it. The installer
            // only creates this rule at fresh-install time if "local network" was chosen then, so widening
            // the tier here at runtime must also open it (or removing it when narrowing back), or remote
            // devices see a socket that's listening but firewalled.
            int listenPort = ServerAddressResolver.ResolveConfiguredPort(configuration);
            if (request.Tier == NetworkTier.Local)
            {
                await firewallRuleManager.RemoveRuleAsync("VideoForensics", ct);
            }
            else
            {
                await firewallRuleManager.EnsureRuleExistsAsync("VideoForensics", listenPort, ct);
            }

            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            await auditLog.LogAsync(SecurityAuditEventTypes.NetworkTierChanged,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), $"{currentTier} -> {request.Tier}", isUrgent: true, ct);

            return Results.Ok(new { requiresRestartToTakeEffect = true });
        }

        private static bool IsValidPort(int port) => port is >= 1 and <= 65535;

        /// <summary>True when an issued Let's Encrypt certificate exists and has not expired. HTTPS ports need one.</summary>
        private static async Task<bool> HasValidCertificateAsync(ILetsEncryptCertificateStore store, CancellationToken ct)
        {
            IssuedCertificate? issued = await store.GetCertificateAsync(ct);
            return issued is not null && issued.NotAfter > DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Returns a message when the port is held by another program. The port this server already listens on for the
        /// scheme is always accepted, since the server's own listener would otherwise look like a conflict.
        /// </summary>
        private static string? PortError(string label, int port, string scheme, IConfiguration configuration)
        {
            int? running = ServerAddressResolver.ResolveListenEndpoints(configuration).FirstOrDefault(e => e.Scheme == scheme)?.Port;
            if (port == running || ServerAddressResolver.IsPortAvailable(port))
            {
                return null;
            }

            return $"Port {port} for {label} is in use by another program. Choose a free port, or keep {running}.";
        }
    }

    public record SetNetworkTierRequest(NetworkTier Tier);
    public record SetInternetServerUrlRequest(string? InternetServerUrl);
    public record SetInternalServerUrlRequest(string? InternalServerUrl);
    public record SetServerPortsRequest(int HttpPort, int HttpsPort);
}
