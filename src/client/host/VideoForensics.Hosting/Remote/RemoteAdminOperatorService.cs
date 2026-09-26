using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IAdminOperatorService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/DeviceManagementEndpoints.cs) for admin-level operator management.
    /// Used both by a thin client (MAUI) talking to a remote server, and by the WebApp's own Blazor UI
    /// calling back into its own Minimal API (see VideoForensics.WebApp.Services.SelfHttpServiceExtensions).
    /// Maps the wire DTO (<see cref="OperatorSummaryDto"/>) to the client-facing <see cref="OperatorSummary"/>
    /// at this HTTP boundary - see that type's doc comment for why the two are kept independent.
    /// </summary>
    public sealed class RemoteAdminOperatorService : IAdminOperatorService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteAdminOperatorService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task UnlockAsync(Guid operatorId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsync($"/api/devices-management/operators/{operatorId}/unlock", null, ct);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<OperatorSummary>> ListOperatorsAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/devices-management/operators", ct);
            _ = response.EnsureSuccessStatusCode();
            List<OperatorSummaryDto>? dtos = await response.Content.ReadFromJsonAsync<List<OperatorSummaryDto>>(JsonOptions, ct);
            return (dtos ?? new List<OperatorSummaryDto>())
                .Select(dto => new OperatorSummary(dto.Id, dto.DisplayName, dto.Active))
                .ToList();
        }
    }
}
