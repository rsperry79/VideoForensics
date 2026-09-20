using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IUpdateCheckService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/UpdateCheckEndpoints.cs) instead of running a local background service.
    /// Part of the "thin client talks to a server API" implementation of the client/server split.
    /// Caches the last known state locally for fast synchronous reads via GetState().
    /// </summary>
    public class RemoteUpdateCheckService : IUpdateCheckService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        /// <summary>
        /// Cached state snapshot - initialized to defaults (no update available, never checked).
        /// Updated whenever TriggerCheckNowAsync completes, either successfully or with an error.
        /// </summary>
        private UpdateCheckState _cachedState = new(
            UpdateAvailable: false,
            LatestVersion: null,
            CurrentVersion: "unknown",
            DownloadUrl: null,
            LastCheckedUtc: null,
            ErrorMessage: null);

        public RemoteUpdateCheckService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public UpdateCheckState GetState()
        {
            // Synchronous read of cached state - no I/O, returns immediately.
            return _cachedState;
        }

        /// <inheritdoc />
        public async Task TriggerCheckNowAsync(CancellationToken ct)
        {
            try
            {
                // POST to the server's check-now endpoint
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    "/api/v1/update-check/check-now",
                    new { },  // empty body
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    // HTTP error: capture the error in cached state but don't throw
                    string errorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                    _cachedState = new UpdateCheckState(
                        UpdateAvailable: false,
                        LatestVersion: null,
                        CurrentVersion: _cachedState.CurrentVersion,
                        DownloadUrl: null,
                        LastCheckedUtc: DateTime.UtcNow,
                        ErrorMessage: errorMessage);
                    return;
                }

                // Deserialize the response
                UpdateCheckStateDto? dto = await response.Content.ReadFromJsonAsync<UpdateCheckStateDto>(JsonOptions, ct);

                if (dto == null)
                {
                    _cachedState = new UpdateCheckState(
                        UpdateAvailable: false,
                        LatestVersion: null,
                        CurrentVersion: _cachedState.CurrentVersion,
                        DownloadUrl: null,
                        LastCheckedUtc: DateTime.UtcNow,
                        ErrorMessage: "Server returned null response");
                    return;
                }

                // Update cached state from the DTO
                _cachedState = dto.ToDomain();
            }
            catch (JsonException jsonEx)
            {
                // Malformed JSON: capture the error in cached state
                _cachedState = new UpdateCheckState(
                    UpdateAvailable: false,
                    LatestVersion: null,
                    CurrentVersion: _cachedState.CurrentVersion,
                    DownloadUrl: null,
                    LastCheckedUtc: DateTime.UtcNow,
                    ErrorMessage: $"Failed to deserialize response: {jsonEx.Message}");
            }
            catch (HttpRequestException httpEx)
            {
                // Network error: capture it in cached state
                _cachedState = new UpdateCheckState(
                    UpdateAvailable: false,
                    LatestVersion: null,
                    CurrentVersion: _cachedState.CurrentVersion,
                    DownloadUrl: null,
                    LastCheckedUtc: DateTime.UtcNow,
                    ErrorMessage: $"Network error: {httpEx.Message}");
            }
            catch (OperationCanceledException)
            {
                // Request was cancelled - don't update state, just propagate
                throw;
            }
            catch (Exception ex)
            {
                // Any other exception: capture it in cached state
                _cachedState = new UpdateCheckState(
                    UpdateAvailable: false,
                    LatestVersion: null,
                    CurrentVersion: _cachedState.CurrentVersion,
                    DownloadUrl: null,
                    LastCheckedUtc: DateTime.UtcNow,
                    ErrorMessage: $"Unexpected error: {ex.Message}");
            }
        }
    }
}
