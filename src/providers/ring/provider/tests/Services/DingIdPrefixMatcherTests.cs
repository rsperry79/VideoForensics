using VideoForensics.Data.Common.Matching;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests.Services
{
    /// <summary>
    /// Tests for DingIdPrefixMatcher. A Ring doorbot history id is exactly equal to the string ding id it belongs to,
    /// so a row matches only a candidate that is equal to it, within one device's candidates. A candidate that merely
    /// shares a prefix does not match.
    /// </summary>
    public class DingIdPrefixMatcherTests
    {
        [Fact]
        public void Match_ExactCandidateEqualsId_ReturnsMatchedWithThatDingId()
        {
            string[] candidates = new[] { "1234567890123456789", "9999999999999999999" };

            DingIdMatchResult result = DingIdPrefixMatcher.Match("1234567890123456789", candidates);

            Assert.Equal(DingIdMatchOutcome.Matched, result.Outcome);
            Assert.Equal("1234567890123456789", result.DingId);
        }

        [Fact]
        public void Match_NoEqualCandidate_ReturnsUnmatchedAndNoDingId()
        {
            string[] candidates = new[] { "9999999999999999999" };

            DingIdMatchResult result = DingIdPrefixMatcher.Match("1234567890123456789", candidates);

            Assert.Equal(DingIdMatchOutcome.Unmatched, result.Outcome);
            Assert.Null(result.DingId);
        }

        [Fact]
        public void Match_CandidateOnlySharesPrefix_ReturnsUnmatched()
        {
            // A shared prefix is not a match under exact equality.
            string[] candidates = new[] { "1234567890123456789" };

            DingIdMatchResult result = DingIdPrefixMatcher.Match("1234567890123456", candidates);

            Assert.Equal(DingIdMatchOutcome.Unmatched, result.Outcome);
            Assert.Null(result.DingId);
        }

        [Fact]
        public void Match_TwoEqualCandidates_ReturnsAmbiguousAndNoDingId()
        {
            string[] candidates = new[] { "1234567890123456789", "1234567890123456789" };

            DingIdMatchResult result = DingIdPrefixMatcher.Match("1234567890123456789", candidates);

            Assert.Equal(DingIdMatchOutcome.Ambiguous, result.Outcome);
            Assert.Null(result.DingId);
        }

        [Fact]
        public void Match_NoCandidates_ReturnsUnmatched()
        {
            DingIdMatchResult result = DingIdPrefixMatcher.Match("1234567890123456789", Array.Empty<string>());

            Assert.Equal(DingIdMatchOutcome.Unmatched, result.Outcome);
        }

        [Fact]
        public void CountOutcomes_MixedResults_ReportsMatchedUnmatchedAndAmbiguousCounts()
        {
            DingIdMatchResult[] results = new[]
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
