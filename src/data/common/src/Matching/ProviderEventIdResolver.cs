using System.Globalization;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Matching
{
    /// <summary>
    /// Decides which provider event id to store for an imported or backfilled row. A numeric id is a doorbot id, which
    /// equals the string ding id, so it is kept only when it is exactly one of the candidate string ding ids of its device.
    /// Non-numeric ids (snapshot-*, legacy ids such as ring_evt_001) are not doorbot ids and are kept as they are.
    /// </summary>
    public static class ProviderEventIdResolver
    {
        /// <summary>Returns the id to store, or null when the row must be skipped.</summary>
        /// <param name="providerEventId">The id stored on the row being imported or backfilled.</param>
        /// <param name="candidates">The string ding ids of the row's device, from <see cref="CandidatesForDeviceAsync"/>.</param>
        public static string? Resolve(string providerEventId, IReadOnlyList<string> candidates)
        {
            ArgumentNullException.ThrowIfNull(providerEventId);
            ArgumentNullException.ThrowIfNull(candidates);

            if (!providerEventId.All(char.IsAsciiDigit))
            {
                return providerEventId;
            }

            // Why: exact equality only. A numeric id that is merely a prefix of a candidate is a different ding, so it is skipped.
            // Two or more equal candidates are ambiguous and skipped, the same rule DingIdPrefixMatcher applies.
            int equal = candidates.Count(c => string.Equals(c, providerEventId, StringComparison.Ordinal));
            return equal == 1 ? providerEventId : null;
        }

        /// <summary>
        /// Returns the string ding ids a device's doorbot history ids can match. The device's provider id is the numeric
        /// doorbot id. Without a device there are no candidates.
        /// </summary>
        /// <param name="device">The device the row belongs to, or null when it is unknown.</param>
        /// <param name="links">The link repository of the open unit of work.</param>
        /// <param name="ct">Cancellation token.</param>
        public static async Task<IReadOnlyList<string>> CandidatesForDeviceAsync(Device? device, IDingIdLinkRepository links, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(links);

            return device is null || !long.TryParse(device.ProviderDeviceId, NumberStyles.None, CultureInfo.InvariantCulture, out long doorbotId)
                ? Array.Empty<string>()
                : await links.GetCandidateDingIdsAsync(doorbotId, ct);
        }
    }
}
