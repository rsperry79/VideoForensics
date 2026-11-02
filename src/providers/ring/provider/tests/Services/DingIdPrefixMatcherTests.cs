using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for DingIdPrefixMatcher. Doorbot history ids are the string ding id with its last three digits dropped,
    /// so a doorbot row matches a string-id ding only by that prefix, within one device's candidates.
    /// </summary>
    public class DingIdPrefixMatcherTests
    {
        [Fact]
        public void Match_DoorbotIdIsPrefixOfOneCandidate_ReturnsMatchedWithThatDingId()
        {
            var candidates = new[] { "1234567890123456789", "9999999999999999999" };

            DingIdMatchResult result = DingIdPrefixMatcher.Match("1234567890123456", candidates);

            Assert.Equal(DingIdMatchOutcome.Matched, result.Outcome);
            Assert.Equal("1234567890123456789", result.DingId);
        }

        [Fact]
        public void Match_NoCandidateHasThePrefix_ReturnsUnmatchedAndNoDingId()
        {
            var candidates = new[] { "9999999999999999999" };

            DingIdMatchResult result = DingIdPrefixMatcher.Match("1234567890123456", candidates);

            Assert.Equal(DingIdMatchOutcome.Unmatched, result.Outcome);
            Assert.Null(result.DingId);
        }

        [Fact]
        public void Match_TwoCandidatesShareThePrefix_ReturnsAmbiguousAndNoDingId()
        {
            var candidates = new[] { "1234567890123456001", "1234567890123456789" };

            DingIdMatchResult result = DingIdPrefixMatcher.Match("1234567890123456", candidates);

            Assert.Equal(DingIdMatchOutcome.Ambiguous, result.Outcome);
            Assert.Null(result.DingId);
        }

        [Fact]
        public void Match_NoCandidates_ReturnsUnmatched()
        {
            DingIdMatchResult result = DingIdPrefixMatcher.Match("1234567890123456", Array.Empty<string>());

            Assert.Equal(DingIdMatchOutcome.Unmatched, result.Outcome);
        }

        [Fact]
        public void Match_CandidateNearInTimeWithoutPrefix_ReturnsUnmatched()
        {
            // Time-based fallback is dropped: a doorbot row must never attach to a ding it does not prefix-match,
            // even if the candidate sits next to it in time. This candidate shares no prefix with the doorbot id.
            var candidates = new[] { "1234567890123457000" };

            DingIdMatchResult result = DingIdPrefixMatcher.Match("1234567890123456", candidates);

            Assert.Equal(DingIdMatchOutcome.Unmatched, result.Outcome);
        }

        [Fact]
        public void CountOutcomes_MixedResults_ReportsMatchedUnmatchedAndAmbiguousCounts()
        {
            var results = new[]
            {
                new DingIdMatchResult(DingIdMatchOutcome.Matched, "1234567890123456789"),
                new DingIdMatchResult(DingIdMatchOutcome.Matched, "1234567890123457111"),
                new DingIdMatchResult(DingIdMatchOutcome.Unmatched, null),
                new DingIdMatchResult(DingIdMatchOutcome.Ambiguous, null),
            };

            DingIdMatchCounts counts = DingIdPrefixMatcher.CountOutcomes(results);

            Assert.Equal(2, counts.Matched);
            Assert.Equal(1, counts.Unmatched);
            Assert.Equal(1, counts.Ambiguous);
        }

        [Fact]
        public void CountOutcomes_NoRows_ReturnsZeroCounts()
        {
            DingIdMatchCounts counts = DingIdPrefixMatcher.CountOutcomes(Array.Empty<DingIdMatchResult>());

            Assert.Equal(0, counts.Matched);
            Assert.Equal(0, counts.Unmatched);
            Assert.Equal(0, counts.Ambiguous);
        }
    }
}
