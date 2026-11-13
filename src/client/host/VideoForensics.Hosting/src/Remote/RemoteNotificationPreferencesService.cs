using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="INotificationPreferencesService"/> that calls the server's Minimal API
    /// (<c>/api/v1/notifications/preferences</c>). Maps the wire DTO (<see cref="NotificationPreferencesDto"/>) to the
    /// client-facing <see cref="NotificationPreferencesSummary"/> at this HTTP boundary - see that type's doc comment for why the two are kept independent.
    /// </summary>
    public sealed class RemoteNotificationPreferencesService : INotificationPreferencesService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteNotificationPreferencesService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<NotificationPreferencesSummary> GetAsync(CancellationToken ct)
        {
            NotificationPreferencesDto? dto = await _httpClient.GetFromJsonAsync<NotificationPreferencesDto>("/api/v1/notifications/preferences", JsonOptions, ct);
            return ToSummary(dto ?? throw new HttpRequestException("The server returned an empty notification preferences response."));
        }

        /// <inheritdoc />
        public async Task<NotificationPreferencesSummary> SaveAsync(NotificationPreferencesSummary preferences, CancellationToken ct)
        {
            var body = new NotificationPreferencesDto(
                PushEnabled: preferences.PushEnabled,
                MinimumSeverity: preferences.MinimumSeverity,
                EmailEnabled: preferences.EmailEnabled,
                EmailEventTypes: preferences.EmailEventTypes
            );

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/notifications/preferences", body, cancellationToken: ct);
            _ = response.EnsureSuccessStatusCode();
            NotificationPreferencesDto? dto = await response.Content.ReadFromJsonAsync<NotificationPreferencesDto>(JsonOptions, ct);
            return ToSummary(dto ?? throw new HttpRequestException("The server returned an empty notification preferences response."));
        }

        private static NotificationPreferencesSummary ToSummary(NotificationPreferencesDto dto)
        {
            return new NotificationPreferencesSummary(
                dto.PushEnabled,
                dto.MinimumSeverity,
                dto.EmailEnabled,
                dto.EmailEventTypes);
        }
    }
}
