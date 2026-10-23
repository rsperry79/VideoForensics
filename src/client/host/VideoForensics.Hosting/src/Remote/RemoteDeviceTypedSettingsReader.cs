using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IDeviceTypedSettingsReader"/> that reads the server's typed settings route
    /// (/api/v1/device-config/typed/{deviceId}) and maps the DTO back to the read model.
    /// </summary>
    public class RemoteDeviceTypedSettingsReader : IDeviceTypedSettingsReader
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteDeviceTypedSettingsReader(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<DeviceTypedSettings> GetAsync(Guid deviceId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/device-config/typed/{deviceId}", ct);
            _ = response.EnsureSuccessStatusCode();
            DeviceTypedSettingsDto? dto = await response.Content.ReadFromJsonAsync<DeviceTypedSettingsDto>(JsonOptions, ct);
            if (dto == null)
            {
                return new DeviceTypedSettings(null, [], []);
            }

            DeviceOfflineMotionSettings? offline = dto.OfflineMotion is { } o
                ? new DeviceOfflineMotionSettings
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    Subscribed = o.Subscribed,
                    Enabled = o.Enabled,
                    MaxUploadKb = o.MaxUploadKb,
                    ResolutionP = o.ResolutionP,
                    FrequencyAfterSecs = o.FrequencyAfterSecs,
                    PeriodAfterSecs = o.PeriodAfterSecs,
                }
                : null;
            var types = dto.DetectionTypes
                .Select(t => new DeviceDetectionType { Id = Guid.NewGuid(), DeviceId = deviceId, DetectionType = t.DetectionType, Enabled = t.Enabled, Mode = t.Mode, Record = t.Record, Notification = t.Notification })
                .ToList();
            var thresholds = dto.Thresholds
                .Select(t => new DeviceDetectionThreshold { Id = Guid.NewGuid(), DeviceId = deviceId, DetectionType = t.DetectionType, Threshold = t.Threshold })
                .ToList();
            return new DeviceTypedSettings(offline, types, thresholds);
        }
    }
}
