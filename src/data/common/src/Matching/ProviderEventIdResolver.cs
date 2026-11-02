using System.Globalization;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Data.Common.Matching
{
    /// <summary>
    /// Decides which provider event id to store for an imported or backfilled row. A numeric id may be a truncated doorbot
    /// id, so it is kept only when it already is a candidate string ding id, or when exactly one candidate prefix-matches it.
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

            if (!providerEventId.All(char.IsAsciiDigit) || candidates.Contains(providerEventId, StringComparer.Ordinal))
            {
                return providerEventId;
            }

            DingIdMatchResult match = DingIdPrefixMatcher.Match(providerEventId, candidates);
            return match.Outcome == DingIdMatchOutcome.Matched ? match.DingId : null;
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

            if (device is null || !long.TryParse(device.ProviderDeviceId, NumberStyles.None, CultureInfo.InvariantCulture, out long doorbotId))
            {
                return Array.Empty<string>();
            }

            return await links.GetCandidateDingIdsAsync(doorbotId, ct);
        }
    }
}
