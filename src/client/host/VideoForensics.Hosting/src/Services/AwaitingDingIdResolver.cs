using VideoForensics.Data.Common.Matching;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Client.Hosting.Services
{
    /// <summary>
    /// Finds the string ding id for an event that is waiting for one. The waiting row is keyed by the doorbot history id,
    /// and an exact match against the device's own ding id candidates names the ding. Zero or several matches are not
    /// resolved, so the row keeps waiting.
    /// </summary>
    internal static class AwaitingDingIdResolver
    {
        /// <summary>Returns the matched ding id, or null when there is no single exact match.</summary>
        public static async Task<string?> ResolveAsync(string doorbotHistoryId, long doorbotId, IVideoForensicsDataClient dataClient, CancellationToken ct)
        {
            IReadOnlyList<string> candidates = await dataClient.GetDingIdCandidatesAsync(doorbotId, ct);
            DingIdMatchResult result = DingIdPrefixMatcher.Match(doorbotHistoryId, candidates);
            return result.Outcome == DingIdMatchOutcome.Matched ? result.DingId : null;
        }
    }
}
