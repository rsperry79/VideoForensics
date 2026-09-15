using System.Net.Http.Json;

using VideoForensics.Api.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IRingSelfTestService"/> that calls the server's self-test API endpoints
    /// (VideoForensics.WebApp/Api/SelfTestEndpoints.cs) instead of running tests locally.
    /// Implements the "remote" half of the client/server split for self-test operations.
    ///
    /// All methods POST/GET the corresponding routes with proper status code handling:
    /// - ListEndpointsAsync: GET /api/v1/selftest (deserializes to list of endpoints)
    /// - StartRunAsync: POST /api/v1/selftest/run (returns 202 Accepted on success, 409 Conflict if already running, 403 Forbidden if insufficient permissions)
    /// - GetStatusAsync: GET /api/v1/selftest/status (synchronous status poll)
    /// - GetResultAsync: GET /api/v1/selftest/result (returns 204 No Content while running, 200 with results when complete)
    /// </summary>
    public class RemoteRingSelfTestService : IRingSelfTestService
    {
        private readonly HttpClient _httpClient;

        public RemoteRingSelfTestService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SelfTestEndpointDto>> ListEndpointsAsync(CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/selftest", cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            List<SelfTestEndpointDto> endpoints = await response.Content.ReadFromJsonAsync<List<SelfTestEndpointDto>>(cancellationToken: cancellationToken)
                ?? [];
            return endpoints.AsReadOnly();
        }

        /// <inheritdoc />
        public async Task<SelfTestRunResponseDto> StartRunAsync(SelfTestRunRequestDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    "/api/v1/selftest/run",
                    request,
                    cancellationToken);

                // 202 Accepted: run was queued successfully
                if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    SelfTestRunResponseDto? dto = await TryReadRunResponseAsync(response, cancellationToken);
                    return dto ?? new SelfTestRunResponseDto(Accepted: true);
                }

                // 409 Conflict: already running (response body contains the rejection details)
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    SelfTestRunResponseDto? dto = await TryReadRunResponseAsync(response, cancellationToken);
                    return dto ?? new SelfTestRunResponseDto(Accepted: false, Error: "A self-test run is already in progress.");
                }

                // 403 Forbidden: insufficient permissions for destructive run
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return new SelfTestRunResponseDto(
                        Accepted: false,
                        Error: "You do not have permission to run destructive self-tests. Destructive tests require SuperAdmin role and local network access."
                    );
                }

                // Any other non-success status: treat as an error
                _ = response.EnsureSuccessStatusCode();
                return new SelfTestRunResponseDto(Accepted: false, Error: "Unexpected response from server.");
            }
            catch (HttpRequestException ex)
            {
                return new SelfTestRunResponseDto(
                    Accepted: false,
                    Error: $"Failed to start self-test run: {ex.Message}"
                );
            }
        }

        /// <summary>Reads a <see cref="SelfTestRunResponseDto"/> from the response body, tolerating an empty or non-JSON body.</summary>
        private static async Task<SelfTestRunResponseDto?> TryReadRunResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                return await response.Content.ReadFromJsonAsync<SelfTestRunResponseDto>(cancellationToken: cancellationToken);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<SelfTestStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/selftest/status", cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            SelfTestStatusDto status = await response.Content.ReadFromJsonAsync<SelfTestStatusDto>(cancellationToken: cancellationToken)
                ?? new SelfTestStatusDto(SelfTestRunStatus.Idle);
            return status;
        }

        /// <inheritdoc />
        public async Task<SelfTestResultDto?> GetResultAsync(CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/selftest/result", cancellationToken);

            // 204 No Content: no result yet (run still in progress or not yet completed)
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return null;
            }

            // 200 OK: result is available
            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SelfTestResultDto>(cancellationToken: cancellationToken);
        }
    }
}
