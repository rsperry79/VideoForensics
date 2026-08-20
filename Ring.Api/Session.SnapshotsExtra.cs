using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Additional snapshot/footage retrieval paths beyond GetLatestSnapshot(): fetching a specific
    /// snapshot by UUID, forcing a fresh capture from the dedicated app-snaps host, and pulling a
    /// periodical footage clip. Endpoints mirror ring-client-api - not confirmed against a live
    /// capture.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Base Uri for Ring's dedicated snapshot delivery host, separate from api.ring.com.
        /// </summary>
        public Uri AppSnapsApiBaseUrl => new Uri("https://app-snaps.ring.com/");

        /// <summary>
        /// Base Uri for Ring's public recordings/footage host.
        /// </summary>
        public Uri RingRecordingsApiBaseUrl => new Uri("https://api.ring.com/recordings/public/");

        /// <summary>
        /// Returns a previously captured snapshot identified by its UUID.
        /// </summary>
        /// <param name="uuid">UUID of the snapshot to retrieve</param>
        public async Task<Stream> GetSnapshotByUuid(string uuid, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"snapshots/uuid?uuid={uuid}");
            var bytes = await _httpUtility.DownloadFile(uri, AuthenticationToken, cancellationToken);
            return new MemoryStream(bytes);
        }

        /// <summary>
        /// Requests and returns the next fresh snapshot from a doorbot via Ring's dedicated
        /// app-snaps delivery host (distinct from GetLatestSnapshot's clients_api/snapshots/image
        /// path, and from UpdateSnapshot's fire-and-forget refresh trigger).
        /// </summary>
        /// <param name="doorbotId">ID of the doorbot to retrieve the next snapshot from</param>
        public async Task<Stream> GetNextSnapshot(long doorbotId, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(AppSnapsApiBaseUrl, $"snapshots/next/{doorbotId}");
            var bytes = await _httpUtility.DownloadFile(uri, AuthenticationToken, cancellationToken);
            return new MemoryStream(bytes);
        }

        /// <summary>
        /// Requests download info for a periodical footage clip by its identifier.
        /// </summary>
        /// <param name="footageId">Identifier of the footage clip</param>
        public async Task<DownloadRecording> GetPeriodicalFootage(string footageId, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(RingRecordingsApiBaseUrl, $"footages/{footageId}");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, cancellationToken);

            return JsonSerializer.Deserialize<DownloadRecording>(response);
        }
    }
}
