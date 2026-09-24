using VideoForensics.Hosting.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IAdminOperatorService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/DeviceManagementEndpoints.cs) for admin-level operator management.
    /// The client (MAUI or other thin client) uses this as part of the client/server split.
    /// </summary>
    public sealed class RemoteAdminOperatorService : IAdminOperatorService
    {
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
    }
}
