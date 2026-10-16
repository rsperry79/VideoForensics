using System;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring
{
    public partial class Session
    {
        /// <summary>
        /// Gets location history from the Ring app API (<c>rs/history</c>, a read-only GET) and returns the raw JSON.
        /// Distinct from the evm/v2 location history in <see cref="GetLocationHistory"/>.
        /// </summary>
        /// <param name="locationId">ID of the location, sent as the <c>accountId</c> query parameter.</param>
        /// <param name="limit">Optional maximum number of entries; omitted from the query when null.</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        public async Task<string> GetAppLocationHistoryAsync(Guid locationId, int? limit = null, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid();

            string query = $"rs/history?accountId={locationId:D}";
            if (limit.HasValue)
            {
                query += $"&limit={limit.Value}";
            }

            var uri = new Uri(RingAppApiBaseUrl, query);
            return await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, cancellationToken);
        }
    }
}
