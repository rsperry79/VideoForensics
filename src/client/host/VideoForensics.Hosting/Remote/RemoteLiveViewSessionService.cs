using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="ILiveViewSessionService"/> that calls the server's Minimal API under
    /// `/api/v1/live-view/...`. This is the MAUI client's implementation of the live-view lifecycle so the
    /// server owns session state. Auth is applied globally by PairedDeviceAuthHandler, not per call.
    /// </summary>
    public class RemoteLiveViewSessionService : ILiveViewSessionService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteLiveViewSessionService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<LiveViewSession> StartAsync(
            Guid deviceId,
            LiveViewTriggerReason reason,
            Guid? operatorId,
            CancellationToken ct)
        {
            // operatorId is intentionally not sent: the server derives the operator from the auth context.
            var request = new StartLiveViewRequestDto(deviceId, reason.ToString());
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/live-view/start", request, JsonOptions, ct);
            _ = response.EnsureSuccessStatusCode();
            LiveViewSessionDto? dto = await response.Content.ReadFromJsonAsync<LiveViewSessionDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Response was null")).ToDomain();
        }

        /// <inheritdoc />
        public async Task ExtendAsync(Guid sessionId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsync($"/api/v1/live-view/{sessionId:D}/extend", null, ct);
            ThrowIfUnknownSession(response, sessionId);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task<LiveViewSession> PromoteToSustainedAsync(Guid sessionId, string reason, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/api/v1/live-view/{sessionId:D}/promote", reason, JsonOptions, ct);
            ThrowIfUnknownSession(response, sessionId);
            _ = response.EnsureSuccessStatusCode();
            LiveViewSessionDto? dto = await response.Content.ReadFromJsonAsync<LiveViewSessionDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Response was null")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<LiveViewSession> DemoteFromSustainedAsync(Guid sessionId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsync($"/api/v1/live-view/{sessionId:D}/demote", null, ct);
            ThrowIfUnknownSession(response, sessionId);
            _ = response.EnsureSuccessStatusCode();
            LiveViewSessionDto? dto = await response.Content.ReadFromJsonAsync<LiveViewSessionDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Response was null")).ToDomain();
        }

        /// <inheritdoc />
        public async Task StopAsync(Guid sessionId, string stopReason, CancellationToken ct)
        {
            var request = new StopLiveViewRequestDto(stopReason);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/api/v1/live-view/{sessionId:D}/stop", request, JsonOptions, ct);
            ThrowIfUnknownSession(response, sessionId);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task<LiveViewSession?> GetActiveSessionAsync(Guid deviceId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/live-view/device/{deviceId:D}/active", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // The server answers 404 when no active session exists for the device; that is a null result, not an error.
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            LiveViewSessionDto? dto = await response.Content.ReadFromJsonAsync<LiveViewSessionDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Response was null")).ToDomain();
        }

        /// <inheritdoc />
        /// <remarks>
        /// Always throws: a live provider connection is an in-process object and cannot cross the wire.
        /// Browser/media consumers must use the server-side signaling hub instead.
        /// </remarks>
        public Task<ILiveViewConnection?> GetConnectionForSessionAsync(Guid sessionId, CancellationToken ct)
        {
            throw new NotSupportedException("Live provider connections cannot cross the wire; use the server-side signaling hub.");
        }

        private static void ThrowIfUnknownSession(HttpResponseMessage response, Guid sessionId)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException($"Live-view session {sessionId:D} was not found.");
            }
        }
    }

    /// <summary>Extension methods for mapping live-view session DTOs back to domain entities.</summary>
    internal static class LiveViewSessionDtoToDomainMapping
    {
        /// <summary>Converts a LiveViewSessionDto to a LiveViewSession entity, parsing enum names.</summary>
        internal static LiveViewSession ToDomain(this LiveViewSessionDto dto)
        {
            return new LiveViewSession
            {
                Id = dto.Id,
                DeviceId = dto.DeviceId,
                TriggerReason = Enum.Parse<LiveViewTriggerReason>(dto.TriggerReason),
                State = Enum.Parse<LiveViewSessionState>(dto.State),
                StartedAtUtc = dto.StartedAtUtc,
                EndedAtUtc = dto.EndedAtUtc,
                LastExtendedAtUtc = dto.LastExtendedAtUtc,
                IsSustained = dto.IsSustained,
                SustainedSinceUtc = dto.SustainedSinceUtc,
                PromotionReason = dto.PromotionReason,
                OperatorId = dto.OperatorId,
                StopReason = dto.StopReason,
                ProviderSessionRef = dto.ProviderSessionRef
            };
        }
    }
}
