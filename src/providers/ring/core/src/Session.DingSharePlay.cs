using System;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Self-test probe for the clients-API share/play path (<c>/clients_api/dings/{id}/share/play</c>),
    /// which returns a playable video URL for a ding (as used by ring-client-api ring-camera.ts).
    /// Read-only; exists to compare against the ding-recording probe (Session.DingRecording.cs).
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Requests the share/play info for a ding from the clients API (read-only GET, redirect disabled)
        /// and returns the raw response body.
        /// </summary>
        /// <param name="dingId">ID of the ding whose playable URL is requested.</param>
        /// <param name="ct">Cancellation token forwarded to the session check and the HTTP call.</param>
        /// <returns>The raw response body exactly as Ring returned it.</returns>
        public async Task<string> GetDingSharePlayAsync(long dingId, CancellationToken ct = default)
        {
            await EnsureSessionValid(ct);

            // disable_redirect=true makes Ring return the URL in the body instead of a 3xx redirect.
            var uri = new Uri(BaseUrl, $"dings/{dingId}/share/play?disable_redirect=true");
            return await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, ct);
        }
    }
}
