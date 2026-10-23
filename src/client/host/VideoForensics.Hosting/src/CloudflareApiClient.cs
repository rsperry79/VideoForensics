using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Client.Hosting
{
    /// <summary>
    /// Thin client over the Cloudflare v4 REST API, authenticated with an Account API token. Every method
    /// reports API failures in its result instead of throwing, so callers can show the message to the operator.
    /// Tokens are sent only in the Authorization header and never appear in results or error messages.
    /// </summary>
    public interface ICloudflareApiClient
    {
        /// <summary>Validates the token and lists the active zones it can see, including each zone's account.</summary>
        Task<CloudflareZoneResult> ValidateTokenAndListZonesAsync(string apiToken, CancellationToken ct);

        /// <summary>Creates a remotely managed tunnel under the account and returns its id and permanent token.</summary>
        Task<CloudflareTunnelResult> CreateTunnelAsync(string apiToken, string accountId, string tunnelName, CancellationToken ct);

        /// <summary>Sets the tunnel's ingress so <paramref name="hostname"/> routes to <paramref name="serviceUrl"/>.</summary>
        Task<CloudflareOperationResult> ConfigureIngressAsync(string apiToken, string accountId, string tunnelId, string hostname, string serviceUrl, CancellationToken ct);

        /// <summary>Creates or updates the proxied CNAME that points <paramref name="hostname"/> at the tunnel.</summary>
        Task<CloudflareOperationResult> UpsertTunnelDnsRecordAsync(string apiToken, string zoneId, string hostname, string tunnelId, CancellationToken ct);

        /// <summary>Creates a TXT record, returning its id so it can be removed later (used for ACME DNS-01 challenges).</summary>
        Task<CloudflareRecordResult> CreateTxtRecordAsync(string apiToken, string zoneId, string name, string value, CancellationToken ct);

        /// <summary>Deletes a DNS record by id.</summary>
        Task<CloudflareOperationResult> DeleteDnsRecordAsync(string apiToken, string zoneId, string recordId, CancellationToken ct);
    }

    /// <summary>A zone visible to the token. <see cref="AccountId"/> is the account that owns the zone.</summary>
    public sealed record CloudflareZone(string Id, string Name, string? AccountId);

    public sealed record CloudflareZoneResult(
        bool IsValid,
        IReadOnlyList<CloudflareZone> Zones,
        string? ErrorMessage);

    public sealed record CloudflareTunnelResult(bool IsSuccess, string? TunnelId, string? TunnelToken, string? ErrorMessage);

    public sealed record CloudflareOperationResult(bool IsSuccess, string? ErrorMessage);

    public sealed record CloudflareRecordResult(bool IsSuccess, string? RecordId, string? ErrorMessage);

    public sealed class CloudflareApiClient : ICloudflareApiClient
    {
        private const string TunnelCnameTarget = "cfargotunnel.com";
        private const string CatchAllService = "http_status:404";

        private readonly HttpClient _httpClient;

        public CloudflareApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<CloudflareZoneResult> ValidateTokenAndListZonesAsync(string apiToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(apiToken))
            {
                return new(false, Array.Empty<CloudflareZone>(), "Enter a Cloudflare API token.");
            }

            ApiCall<CloudflareZoneDto[]> call = await CallAsync<CloudflareZoneDto[]>(
                HttpMethod.Get, "client/v4/zones?per_page=50&status=active", apiToken, null, ct);
            if (!call.IsSuccess)
            {
                return new(false, Array.Empty<CloudflareZone>(), call.ErrorMessage ?? "Cloudflare rejected the API token.");
            }

            // Malformed entries (no id or name) are dropped rather than failing the whole list.
            CloudflareZone[] zones = (call.Result ?? Array.Empty<CloudflareZoneDto>())
                .Where(zone => !string.IsNullOrWhiteSpace(zone.Id) && !string.IsNullOrWhiteSpace(zone.Name))
                .Select(zone => new CloudflareZone(zone.Id!, zone.Name!, zone.Account?.Id))
                .OrderBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new(true, zones, null);
        }

        public async Task<CloudflareTunnelResult> CreateTunnelAsync(string apiToken, string accountId, string tunnelName, CancellationToken ct)
        {
            string accountPath = AccountTunnelsPath(accountId);

            ApiCall<CloudflareTunnelDto> created = await CallAsync<CloudflareTunnelDto>(
                HttpMethod.Post, accountPath, apiToken, new { name = tunnelName, config_src = "cloudflare" }, ct);
            if (!created.IsSuccess || string.IsNullOrWhiteSpace(created.Result?.Id))
            {
                return new(false, null, null, created.ErrorMessage ?? "Cloudflare did not return a tunnel id.");
            }

            string tunnelId = created.Result.Id!;
            ApiCall<string> token = await CallAsync<string>(
                HttpMethod.Get, $"{accountPath}/{Uri.EscapeDataString(tunnelId)}/token", apiToken, null, ct);
            return !token.IsSuccess || string.IsNullOrWhiteSpace(token.Result)
                ? new(false, tunnelId, null, token.ErrorMessage ?? "Cloudflare did not return a tunnel token.")
                : new(true, tunnelId, token.Result, null);
        }

        public async Task<CloudflareOperationResult> ConfigureIngressAsync(string apiToken, string accountId, string tunnelId, string hostname, string serviceUrl, CancellationToken ct)
        {
            // The catch-all rule is required: Cloudflare rejects an ingress list that does not end with one.
            var body = new
            {
                config = new
                {
                    ingress = new object[]
                    {
                        new { hostname, service = serviceUrl },
                        new { service = CatchAllService },
                    },
                },
            };

            ApiCall<JsonElement> call = await CallAsync<JsonElement>(
                HttpMethod.Put, $"{AccountTunnelsPath(accountId)}/{Uri.EscapeDataString(tunnelId)}/configurations", apiToken, body, ct);
            return new(call.IsSuccess, call.ErrorMessage);
        }

        public async Task<CloudflareOperationResult> UpsertTunnelDnsRecordAsync(string apiToken, string zoneId, string hostname, string tunnelId, CancellationToken ct)
        {
            string recordsPath = $"client/v4/zones/{Uri.EscapeDataString(zoneId)}/dns_records";
            ApiCall<CloudflareDnsRecordDto[]> existing = await CallAsync<CloudflareDnsRecordDto[]>(
                HttpMethod.Get, $"{recordsPath}?type=CNAME&name={Uri.EscapeDataString(hostname)}", apiToken, null, ct);
            if (!existing.IsSuccess)
            {
                return new(false, existing.ErrorMessage);
            }

            var record = new
            {
                type = "CNAME",
                name = hostname,
                content = $"{tunnelId}.{TunnelCnameTarget}",
                proxied = true,
                ttl = 1,
            };

            // ttl 1 means "automatic" in the Cloudflare API.
            string? recordId = existing.Result?.FirstOrDefault()?.Id;
            ApiCall<JsonElement> call = string.IsNullOrWhiteSpace(recordId)
                ? await CallAsync<JsonElement>(HttpMethod.Post, recordsPath, apiToken, record, ct)
                : await CallAsync<JsonElement>(HttpMethod.Put, $"{recordsPath}/{Uri.EscapeDataString(recordId)}", apiToken, record, ct);
            return new(call.IsSuccess, call.ErrorMessage);
        }

        public async Task<CloudflareRecordResult> CreateTxtRecordAsync(string apiToken, string zoneId, string name, string value, CancellationToken ct)
        {
            ApiCall<CloudflareDnsRecordDto> call = await CallAsync<CloudflareDnsRecordDto>(
                HttpMethod.Post,
                $"client/v4/zones/{Uri.EscapeDataString(zoneId)}/dns_records",
                apiToken,
                new { type = "TXT", name, content = value, ttl = 60 },
                ct);
            return string.IsNullOrWhiteSpace(call.Result?.Id)
                ? new(false, null, call.ErrorMessage ?? "Cloudflare did not return a record id.")
                : new(true, call.Result.Id, null);
        }

        public async Task<CloudflareOperationResult> DeleteDnsRecordAsync(string apiToken, string zoneId, string recordId, CancellationToken ct)
        {
            ApiCall<JsonElement> call = await CallAsync<JsonElement>(
                HttpMethod.Delete,
                $"client/v4/zones/{Uri.EscapeDataString(zoneId)}/dns_records/{Uri.EscapeDataString(recordId)}",
                apiToken,
                null,
                ct);
            return new(call.IsSuccess, call.ErrorMessage);
        }

        private static string AccountTunnelsPath(string accountId)
        {
            return $"client/v4/accounts/{Uri.EscapeDataString(accountId)}/cfd_tunnel";
        }

        private async Task<ApiCall<T>> CallAsync<T>(HttpMethod method, string path, string apiToken, object? body, CancellationToken ct)
        {
            try
            {
                using var request = new HttpRequestMessage(method, path);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
                if (body is not null)
                {
                    request.Content = JsonContent.Create(body);
                }

                using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
                CloudflareApiResponse<T>? payload = await response.Content.ReadFromJsonAsync<CloudflareApiResponse<T>>(cancellationToken: ct);
                if (!response.IsSuccessStatusCode || payload?.Success != true)
                {
                    string? error = payload?.Errors?.FirstOrDefault()?.Message;
                    return new(false, default, error ?? $"Cloudflare returned HTTP {(int)response.StatusCode}.");
                }

                return new(true, payload.Result, null);
            }
            catch (JsonException)
            {
                return new(false, default, "Cloudflare returned an unreadable response.");
            }
            catch (HttpRequestException)
            {
                return new(false, default, "Could not reach the Cloudflare API.");
            }
        }

        private sealed record ApiCall<T>(bool IsSuccess, T? Result, string? ErrorMessage);

        private sealed class CloudflareApiResponse<T>
        {
            [JsonPropertyName("success")]
            public bool Success { get; init; }

            [JsonPropertyName("result")]
            public T? Result { get; init; }

            [JsonPropertyName("errors")]
            public CloudflareApiError[]? Errors { get; init; }
        }

        private sealed class CloudflareApiError
        {
            [JsonPropertyName("message")]
            public string? Message { get; init; }
        }

        private sealed class CloudflareZoneDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; init; }

            [JsonPropertyName("name")]
            public string? Name { get; init; }

            [JsonPropertyName("account")]
            public CloudflareAccountDto? Account { get; init; }
        }

        private sealed class CloudflareAccountDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; init; }
        }

        private sealed class CloudflareTunnelDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; init; }
        }

        private sealed class CloudflareDnsRecordDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; init; }
        }
    }
}
