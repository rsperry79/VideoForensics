namespace VideoForensics.Data.Common.Matching
{
    /// <summary>How a doorbot history row relates to the string-id dings of one device.</summary>
    public enum DingIdMatchOutcome
    {
        /// <summary>Exactly one candidate ding is equal to the doorbot id.</summary>
        Matched,

        /// <summary>No candidate ding is equal to the doorbot id.</summary>
        Unmatched,

        /// <summary>More than one candidate ding is equal to the doorbot id. Nothing is linked.</summary>
        Ambiguous,
    }

    /// <summary>The result of matching one doorbot id. DingId is set only when Outcome is Matched.</summary>
    /// <param name="Outcome">Whether the doorbot id matched zero, one or several candidates.</param>
    /// <param name="DingId">The matched string ding id, or null.</param>
    public sealed record DingIdMatchResult(DingIdMatchOutcome Outcome, string? DingId);

    /// <summary>Per-feed totals of match outcomes, reported by the migration.</summary>
    /// <param name="Matched">Rows with exactly one equal candidate.</param>
    /// <param name="Unmatched">Rows with no equal candidate.</param>
    /// <param name="Ambiguous">Rows with more than one equal candidate.</param>
    public sealed record DingIdMatchCounts(int Matched, int Unmatched, int Ambiguous);

    /// <summary>
    /// Links a doorbot history id to a string ding id by exact equality. Ring's doorbot history id is the same 19-digit
    /// value as the string ding id it belongs to, so a candidate matches only when it equals the id. Callers pass only
    /// the candidates of one device, resolved from the device table first; matching across devices is not allowed.
    /// There is no time-window fallback: motion bursts on one device can fall inside any short window.
    /// The class name is historical; it no longer matches on a prefix.
    /// </summary>
    public static class DingIdPrefixMatcher
    {
        /// <summary>Matches one doorbot id against the string ding ids of its device by exact equality.</summary>
        /// <param name="doorbotId">The numeric doorbot history id, as text.</param>
        /// <param name="candidateDingIds">The string ding ids of the same device.</param>
        /// <returns>The outcome, with the ding id when exactly one candidate is equal.</returns>
        public static DingIdMatchResult Match(string doorbotId, IEnumerable<string> candidateDingIds)
        {
            ArgumentException.ThrowIfNullOrEmpty(doorbotId);
            ArgumentNullException.ThrowIfNull(candidateDingIds);

            string? matched = null;
            int count = 0;
            foreach (string candidate in candidateDingIds)
            {
                // Why: a shared prefix is not a match. Ring ids are exact, so a longer id that starts with the same digits is a different ding.
                if (!string.Equals(candidate, doorbotId, StringComparison.Ordinal))
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
