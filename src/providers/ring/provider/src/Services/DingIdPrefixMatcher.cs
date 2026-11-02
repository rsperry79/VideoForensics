namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>How a doorbot history row relates to the string-id dings of one device.</summary>
    public enum DingIdMatchOutcome
    {
        /// <summary>Exactly one candidate ding has this doorbot id as its prefix.</summary>
        Matched,

        /// <summary>No candidate ding has this doorbot id as its prefix.</summary>
        Unmatched,

        /// <summary>More than one candidate ding has this doorbot id as its prefix. Nothing is linked.</summary>
        Ambiguous,
    }

    /// <summary>The result of matching one doorbot id. DingId is set only when Outcome is Matched.</summary>
    /// <param name="Outcome">Whether the doorbot id matched zero, one or several candidates.</param>
    /// <param name="DingId">The matched string ding id, or null.</param>
    public sealed record DingIdMatchResult(DingIdMatchOutcome Outcome, string? DingId);

    /// <summary>Per-feed totals of match outcomes, reported by the migration.</summary>
    /// <param name="Matched">Rows with exactly one candidate.</param>
    /// <param name="Unmatched">Rows with no candidate.</param>
    /// <param name="Ambiguous">Rows with more than one candidate.</param>
    public sealed record DingIdMatchCounts(int Matched, int Unmatched, int Ambiguous);

    /// <summary>
    /// Links a doorbot history id to a string ding id. Ring's numeric doorbot id is the string ding id with its last
    /// three digits dropped (truncated, not rounded), so the match is exact on that prefix. Callers pass only the
    /// candidates of one device, resolved from the device table first; matching across devices is not allowed.
    /// There is no time-window fallback: motion bursts on one device can fall inside any short window.
    /// </summary>
    public static class DingIdPrefixMatcher
    {
        // Ring drops exactly three trailing digits from the string ding id to form the numeric doorbot id.
        private const int TruncatedDigits = 3;

        /// <summary>Matches one doorbot id against the string ding ids of its device.</summary>
        /// <param name="doorbotId">The numeric doorbot history id, as text.</param>
        /// <param name="candidateDingIds">The string ding ids of the same device.</param>
        /// <returns>The outcome, with the ding id when exactly one candidate matches.</returns>
        public static DingIdMatchResult Match(string doorbotId, IEnumerable<string> candidateDingIds)
        {
            ArgumentException.ThrowIfNullOrEmpty(doorbotId);
            ArgumentNullException.ThrowIfNull(candidateDingIds);

            string? matched = null;
            int count = 0;
            foreach (string candidate in candidateDingIds)
            {
                if (candidate.Length - TruncatedDigits != doorbotId.Length
                    || !candidate.StartsWith(doorbotId, StringComparison.Ordinal))
                {
                    continue;
                }

                matched = candidate;
                count++;
            }

            return count switch
            {
                0 => new DingIdMatchResult(DingIdMatchOutcome.Unmatched, null),
                1 => new DingIdMatchResult(DingIdMatchOutcome.Matched, matched),
                _ => new DingIdMatchResult(DingIdMatchOutcome.Ambiguous, null),
            };
        }

        /// <summary>Totals the outcomes of a batch of matches for the migration report.</summary>
        public static DingIdMatchCounts CountOutcomes(IEnumerable<DingIdMatchResult> results)
        {
            ArgumentNullException.ThrowIfNull(results);

            int matched = 0;
            int unmatched = 0;
            int ambiguous = 0;
            foreach (DingIdMatchResult result in results)
            {
                switch (result.Outcome)
                {
                    case DingIdMatchOutcome.Matched:
                        matched++;
                        break;
                    case DingIdMatchOutcome.Unmatched:
                        unmatched++;
                        break;
                    default:
                        ambiguous++;
                        break;
                }
            }

            return new DingIdMatchCounts(matched, unmatched, ambiguous);
        }
    }
}
