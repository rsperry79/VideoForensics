using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Standalone device health (connectivity/battery telemetry) lookups. The same data is already
    /// embedded in each device returned by GetRingDevices() - these exist for callers that only
    /// need a single device's health without re-fetching the whole device list.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Returns the connectivity/battery health of a single doorbell/camera.
        /// </summary>
        /// <param name="doorbotId">ID of the doorbot to retrieve health for</param>
        public async Task<DeviceHealthResponse> GetDoorbotHealth(long doorbotId, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"doorbots/{doorbotId}/health");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, cancellationToken);

            return JsonSerializer.Deserialize<DeviceHealthResponse>(response);
        }

        /// <summary>
        /// Returns the connectivity health of a single chime.
        /// </summary>
        /// <param name="chimeId">ID of the chime to retrieve health for</param>
        public async Task<DeviceHealthResponse> GetChimeHealth(long chimeId, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"chimes/{chimeId}/health");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, cancellationToken);

            return JsonSerializer.Deserialize<DeviceHealthResponse>(response);
        }
    }
}
