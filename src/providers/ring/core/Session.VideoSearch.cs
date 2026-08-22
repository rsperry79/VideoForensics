using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Date-range-filterable video search, an alternative to GetDoorbotsHistory() when a specific
    /// window is known up front rather than paged backwards via older_than. Endpoint mirrors
    /// ring-client-api's videoSearch() - not confirmed against a live capture.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Searches recorded events for a doorbot within a date range. Confirmed via a live
        /// ApiTester run that Ring replies HTTP 400 when date_from/date_to are omitted, so this
        /// always sends both - defaulting to the last 30 days when the caller doesn't provide one.
        /// </summary>
        /// <param name="doorbotId">ID of the doorbot to search events for</param>
        /// <param name="dateFrom">Start of the date range (inclusive). Defaults to 30 days before dateTo/now.</param>
        /// <param name="dateTo">End of the date range (inclusive). Defaults to now.</param>
        public async Task<List<VideoSearchItem>> VideoSearch(long doorbotId, DateTime? dateFrom = null, DateTime? dateTo = null, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var effectiveDateTo = dateTo ?? DateTime.UtcNow;
            var effectiveDateFrom = dateFrom ?? effectiveDateTo.AddDays(-30);

            var query = $"video_search/history?doorbot_id={doorbotId}" +
                $"&date_from={new DateTimeOffset(effectiveDateFrom).ToUnixTimeMilliseconds()}" +
                $"&date_to={new DateTimeOffset(effectiveDateTo).ToUnixTimeMilliseconds()}";

            var response = await _httpUtility.GetContents(new Uri(BaseUrl, query), AuthenticationToken, _hardwareId, cancellationToken);

            var parsed = JsonSerializer.Deserialize<VideoSearchResponse>(response);
            return parsed?.VideoSearch ?? new List<VideoSearchItem>();
        }
    }
}
