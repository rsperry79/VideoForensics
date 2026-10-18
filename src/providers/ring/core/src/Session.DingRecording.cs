using System;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Self-test probe for the clients-API recording path (<c>/clients_api/dings/{id}/recording</c>),
    /// as used by python-ring-doorbell. Read-only; exists only to compare against the public
    /// recordings path (Session.GetPeriodicalFootage), which returns 403.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Requests the recording for a ding from the clients API (read-only GET) and returns the raw response body.
        /// </summary>
        /// <param name="dingId">ID of the ding whose recording is requested.</param>
        /// <param name="ct">Cancellation token forwarded to the session check and the HTTP call.</param>
        /// <returns>The raw response body exactly as Ring returned it.</returns>
        public async Task<string> GetDingRecordingAsync(long dingId, CancellationToken ct = default)
        {
            await EnsureSessionValid(ct);

            var uri = new Uri(BaseUrl, $"dings/{dingId}/recording");
            return await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, ct);
        }
    }
}
