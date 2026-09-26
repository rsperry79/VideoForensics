using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="ISecurityEventsService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/SecurityEventsEndpoints.cs) instead of accessing the database directly.
    /// Used both by a thin client (MAUI) talking to a remote server, and by the WebApp's own Blazor UI
    /// calling back into its own Minimal API (see VideoForensics.WebApp.Services.SelfHttpServiceExtensions).
    /// Maps the wire DTO (<see cref="SecurityEventDto"/>) to the client-facing <see cref="SecurityEventSummary"/>
    /// at this HTTP boundary - see that type's doc comment for why the two are kept independent.
    /// </summary>
    public sealed class RemoteSecurityEventsService : ISecurityEventsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteSecurityEventsService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SecurityEventSummary>> GetEventsAsync(Guid? operatorId, int offset, int limit, CancellationToken ct)
        {
            var request = new SecurityEventsQueryRequest(
                Limit: limit,
                Offset: offset,
                OperatorId: operatorId?.ToString()
            );

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/security-events", request, cancellationToken: ct);
            _ = response.EnsureSuccessStatusCode();
            List<SecurityEventDto>? dtos = await response.Content.ReadFromJsonAsync<List<SecurityEventDto>>(JsonOptions, ct);
            return (dtos ?? new List<SecurityEventDto>())
                .Select(dto => new SecurityEventSummary(
                    dto.Id,
                    dto.OperatorId,
                    dto.EventType,
                    dto.Success,
                    dto.IpAddress,
                    dto.OccurredAtUtc,
                    dto.Reason))
                .ToList();
        }
    }
}
