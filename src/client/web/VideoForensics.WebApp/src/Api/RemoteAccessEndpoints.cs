using Microsoft.AspNetCore.Http;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Infrastructure;
using VideoForensics.WebApp.Services;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Cloudflare Tunnel setup for the Internet tier. Every route is SuperAdmin-gated; starting a tunnel and
    /// provisioning one (which changes public DNS) also require a fresh step-up. Secrets are never returned
    /// or written to audit records - only hostnames and token presence.
    /// </summary>
    public static class RemoteAccessEndpoints
    {
        public static void MapRemoteAccessEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/remote-access")
                .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey);

            _ = group.MapGet("/status", async (
                ICloudflaredTunnelService tunnels,
                ICloudflareTokenStore tokens,
                IForensicsConfiguration config,
                IConfiguration appConfiguration,
                CancellationToken ct) =>
            {
                // Reports whether each token is present, never its value.
                TunnelState state = tunnels.GetState();
                string? apiToken = await tokens.GetDecryptedApiTokenAsync(ct);
                string? tunnelToken = await tokens.GetDecryptedTunnelTokenAsync(ct);
                return Results.Ok(new
                {
                    cloudflareAuthenticated = !string.IsNullOrWhiteSpace(apiToken),
                    tunnelConfigured = !string.IsNullOrWhiteSpace(tunnelToken),
                    configuredUrl = config.InternetServerUrl,
                    networkTier = config.ConfiguredNetworkTier.ToString(),
                    rpId = ServerAddressResolver.ResolveRpId(appConfiguration, config.InternetServerUrl),
                    passkeyOrigins = PasskeyOrigins(appConfiguration, config.InternetServerUrl),
                    kind = state.Kind.ToString(),
                    status = state.Status.ToString(),
                    publicUrl = state.PublicUrl,
                    errorMessage = state.ErrorMessage,
                    recentLogLines = state.RecentLogLines
                });
            });

            _ = group.MapPost("/token", async (
                SaveCloudflareTokenRequest request,
                ICloudflareApiClient cloudflare,
                ICloudflareTokenStore tokens,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.ApiToken))
                {
                    return Results.BadRequest(new { error = "Enter a Cloudflare API token." });
                }

                CloudflareZoneResult result = await cloudflare.ValidateTokenAndListZonesAsync(request.ApiToken, ct);
                if (!result.IsValid)
                {
                    return Results.BadRequest(new { error = result.ErrorMessage });
                }

                await tokens.SetApiTokenAsync(request.ApiToken.Trim(), ct);
                await AuditAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.CloudflareApiTokenConfigured,
                    $"Cloudflare API token saved; {result.Zones.Count} zone(s) visible", ct);
                return Results.Ok(new { zones = result.Zones.Select(z => new { z.Id, z.Name }) });
            });

            _ = group.MapGet("/zones", async (
                ICloudflareApiClient cloudflare,
                ICloudflareTokenStore tokens,
                CancellationToken ct) =>
            {
                string? token = await tokens.GetDecryptedApiTokenAsync(ct);
                if (string.IsNullOrWhiteSpace(token))
                {
                    return Results.BadRequest(new { error = "Configure a Cloudflare API token first." });
                }

                CloudflareZoneResult result = await cloudflare.ValidateTokenAndListZonesAsync(token, ct);
                return result.IsValid
                    ? Results.Ok(new { zones = result.Zones.Select(z => new { z.Id, z.Name }) })
                    : Results.BadRequest(new { error = result.ErrorMessage });
            });

            // Saves a tunnel token the operator created in the Cloudflare dashboard.
            _ = group.MapPost("/tunnel", async (
                ConfigureCloudflareTunnelRequest request,
                ICloudflareTokenStore tokens,
                ICloudflareApiClient cloudflare,
                IForensicsConfiguration config,
                IForensicsConfigurationService configService,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.TunnelToken)
                    || !IsSafeLabel(request.HostnamePrefix)
                    || !IsSafeDomain(request.Domain))
                {
                    return Results.BadRequest(new { error = "Enter a hostname prefix and select a valid Cloudflare domain." });
                }

                ZoneLookup lookup = await LookUpZoneAsync(tokens, cloudflare, request.Domain, ct);
                if (lookup.Error is not null)
                {
                    return Results.BadRequest(new { error = lookup.Error });
                }

                string fqdn = BuildFqdn(request.HostnamePrefix, request.Domain);
                await tokens.SetTunnelTokenAsync(request.TunnelToken.Trim(), ct);
                await SaveFqdnAsync(config, configService, auditLog, tierResolver, context, fqdn, ct);
                await AuditAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.CloudflareTunnelConfigured,
                    $"Cloudflare tunnel token saved for {fqdn}", ct);
                return Results.Ok(new { fqdn });
            });

            // Creates the tunnel through the Cloudflare API, points its ingress at this server, creates the
            // DNS record, and stores the generated tunnel token. Changes public DNS, so it is step-up gated.
            _ = group.MapPost("/tunnel/provision", async (
                ProvisionCloudflareTunnelRequest request,
                ICloudflareTokenStore tokens,
                ICloudflareApiClient cloudflare,
                IForensicsConfiguration config,
                IForensicsConfigurationService configService,
                IConfiguration appConfiguration,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (!IsSafeLabel(request.HostnamePrefix) || !IsSafeDomain(request.Domain))
                {
                    return Results.BadRequest(new { error = "Enter a hostname prefix and select a valid Cloudflare domain." });
                }

                ZoneLookup lookup = await LookUpZoneAsync(tokens, cloudflare, request.Domain, ct);
                if (lookup.Error is not null || lookup.Zone is null || string.IsNullOrWhiteSpace(lookup.Zone.AccountId))
                {
                    return Results.BadRequest(new { error = lookup.Error ?? "The selected domain has no Cloudflare account." });
                }

                string fqdn = BuildFqdn(request.HostnamePrefix, request.Domain);
                string apiToken = lookup.ApiToken!;
                string tunnelName = $"videoforensics-{request.HostnamePrefix.Trim().ToLowerInvariant()}";

                CloudflareTunnelResult tunnel = await cloudflare.CreateTunnelAsync(apiToken, lookup.Zone.AccountId, tunnelName, ct);
                if (!tunnel.IsSuccess || tunnel.TunnelId is null || tunnel.TunnelToken is null)
                {
                    return Results.BadRequest(new { error = "Creating the tunnel failed: " + (tunnel.ErrorMessage ?? "Cloudflare did not create the tunnel.") });
                }

                string serviceUrl = $"http://localhost:{ServerAddressResolver.ResolveHttpPort(appConfiguration)}";
                CloudflareOperationResult ingress = await cloudflare.ConfigureIngressAsync(
                    apiToken, lookup.Zone.AccountId, tunnel.TunnelId, fqdn, serviceUrl, ct);
                if (!ingress.IsSuccess)
                {
                    return Results.BadRequest(new { error = "Setting the tunnel routing failed: " + (ingress.ErrorMessage ?? "Cloudflare did not accept the tunnel ingress.") });
                }

                CloudflareOperationResult dns = await cloudflare.UpsertTunnelDnsRecordAsync(apiToken, lookup.Zone.Id, fqdn, tunnel.TunnelId, ct);
                if (!dns.IsSuccess)
                {
                    return Results.BadRequest(new { error = "Creating the DNS record failed: " + (dns.ErrorMessage ?? "Cloudflare did not create the DNS record.") });
                }

                await tokens.SetTunnelTokenAsync(tunnel.TunnelToken, ct);
                await SaveFqdnAsync(config, configService, auditLog, tierResolver, context, fqdn, ct);
                await AuditAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.CloudflareTunnelConfigured,
                    $"Cloudflare tunnel {tunnelName} provisioned for {fqdn}", ct);
                return Results.Ok(new { fqdn });
            }).AddEndpointFilter<StepUpEndpointFilter>();

            _ = group.MapPost("/start", async (
                ICloudflaredTunnelService tunnels,
                ICloudflareTokenStore tokens,
                IForensicsConfiguration config,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (config.ConfiguredNetworkTier == NetworkTier.Local)
                {
                    return Results.BadRequest(new { error = "Set the network tier to Network or Internet before starting a tunnel." });
                }

                string? token = await tokens.GetDecryptedTunnelTokenAsync(ct);
                if (string.IsNullOrWhiteSpace(token))
                {
                    return Results.BadRequest(new { error = "Configure the Cloudflare tunnel token first." });
                }

                await tunnels.StartPermanentTunnelAsync(token, ct);
                await AuditAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.TunnelStarted, "Permanent Cloudflare tunnel started", ct);
                return Results.Ok();
            }).AddEndpointFilter<StepUpEndpointFilter>();

            _ = group.MapPost("/stop", async (
                ICloudflaredTunnelService tunnels,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                await tunnels.StopAsync(ct);
                await AuditAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.TunnelStopped, "Cloudflare tunnel stopped", ct);
                return Results.Ok();
            });

            // Requests a trusted certificate for the Internet hostname now, instead of waiting for the renewal check.
            // Saves the contact email Let's Encrypt needs first. Changes the certificate the server presents, so step-up gated.
            _ = group.MapPost("/certificate", async (
                RequestCertificateRequest request,
                LetsEncryptCertificateManager certificates,
                ILetsEncryptCertificateStore store,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.ContactEmail) || !request.ContactEmail.Contains('@', StringComparison.Ordinal))
                {
                    return Results.BadRequest(new { error = "Enter a valid contact email for Let's Encrypt." });
                }

                await store.SetContactEmailAsync(request.ContactEmail, ct);
                CertificateCheckResult result = await certificates.EnsureCertificateAsync(force: true, ct);
                await AuditAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.CertificateIssued,
                    result.Success ? result.Message : "Certificate request failed: " + result.Message, ct);
                return result.Success
                    ? Results.Ok(new { message = result.Message, issued = result.Issued })
                    : Results.BadRequest(new { error = result.Message });
            }).AddEndpointFilter<StepUpEndpointFilter>();

            // Stops the tunnel and deletes both stored Cloudflare tokens. The configured address is kept so
            // that re-connecting only needs the tokens again.
            _ = group.MapPost("/disconnect", async (
                ICloudflaredTunnelService tunnels,
                ICloudflareTokenStore tokens,
                ISecurityAuditLogger auditLog,
                INetworkTierResolver tierResolver,
                HttpContext context,
                CancellationToken ct) =>
            {
                await tunnels.StopAsync(ct);
                await tokens.ClearAsync(ct);
                await AuditAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.CloudflareDisconnected,
                    "Cloudflare tokens cleared and tunnel stopped", ct);
                return Results.Ok();
            });
        }

        /// <summary>
        /// Hostname labels are letters, digits and hyphens, 1-63 characters, and cannot start or end with a hyphen.
        /// </summary>
        internal static bool IsSafeLabel(string? value)
        {
            string label = value?.Trim() ?? string.Empty;
            return label.Length is >= 1 and <= 63
                && label[0] != '-'
                && label[^1] != '-'
                && label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
        }

        internal static bool IsSafeDomain(string? value)
            => Uri.TryCreate($"https://{value}", UriKind.Absolute, out Uri? uri)
                && uri.Host == value?.Trim().TrimEnd('.').ToLowerInvariant()
                && uri.Host.Contains('.', StringComparison.Ordinal);

        /// <summary>Builds the lower-case public hostname from a validated prefix and domain.</summary>
        internal static string BuildFqdn(string prefix, string domain)
            => $"{prefix.Trim().ToLowerInvariant()}.{domain.Trim().TrimEnd('.').ToLowerInvariant()}";

        /// <summary>
        /// Loads the stored API token and confirms the domain is one of the zones that token can see. The
        /// domain is never trusted from the request alone.
        /// </summary>
        internal static async Task<ZoneLookup> LookUpZoneAsync(
            ICloudflareTokenStore tokens,
            ICloudflareApiClient cloudflare,
            string domain,
            CancellationToken ct)
        {
            string? apiToken = await tokens.GetDecryptedApiTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(apiToken))
            {
                return new ZoneLookup(null, null, "Configure a Cloudflare API token first.");
            }

            CloudflareZoneResult zones = await cloudflare.ValidateTokenAndListZonesAsync(apiToken, ct);
            if (!zones.IsValid)
            {
                return new ZoneLookup(apiToken, null, zones.ErrorMessage ?? "Cloudflare rejected the stored API token.");
            }

            CloudflareZone? zone = zones.Zones.FirstOrDefault(z => string.Equals(z.Name, domain.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
            return zone is null
                ? new ZoneLookup(apiToken, null, "Select a domain returned by your Cloudflare token.")
                : new ZoneLookup(apiToken, zone, null);
        }

        /// <summary>The origins a passkey ceremony may come from: the local listeners plus the saved Internet address.</summary>
        internal static string[] PasskeyOrigins(IConfiguration configuration, string? internetServerUrl)
        {
            var origins = ServerAddressResolver.ResolveAllowedOrigins(configuration).ToList();
            if (Uri.TryCreate(internetServerUrl, UriKind.Absolute, out Uri? internetUrl))
            {
                origins.Add(internetUrl.GetLeftPart(UriPartial.Authority));
            }

            return origins.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        }

        private static async Task SaveFqdnAsync(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            string fqdn,
            CancellationToken ct)
        {
            string url = $"https://{fqdn}";
            string? oldUrl = config.InternetServerUrl;
            config.InternetServerUrl = url;
            await configService.SaveConfigurationAsync(config, ct);
            await AuditAsync(auditLog, tierResolver, context, SecurityAuditEventTypes.InternetServerUrlChanged,
                $"Internet address changed from '{oldUrl}' to '{url}'", ct);
        }

        private static Task AuditAsync(
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            string eventType,
            string details,
            CancellationToken ct)
        {
            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            return auditLog.LogAsync(eventType,
                Guid.TryParse(operatorIdClaim, out Guid actingOperatorId) ? actingOperatorId : null,
                null, tierResolver.ResolveClientIp(context), details, isUrgent: true, ct);
        }

        internal sealed record ZoneLookup(string? ApiToken, CloudflareZone? Zone, string? Error);
    }

    public sealed record SaveCloudflareTokenRequest(string ApiToken);
    public sealed record ConfigureCloudflareTunnelRequest(string TunnelToken, string HostnamePrefix, string Domain);
    public sealed record ProvisionCloudflareTunnelRequest(string HostnamePrefix, string Domain);
    public sealed record RequestCertificateRequest(string ContactEmail);
}
