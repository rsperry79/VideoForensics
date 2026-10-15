using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Tests for <see cref="CaseTypeClassifier"/>, which derives the case type from the case number prefix.</summary>
    public class CaseTypeClassifierTests
    {
        [Fact]
        public void FromCaseNumber_DetectedPrefix_ReturnsDetected()
        {
            Assert.Equal("Detected", CaseTypeClassifier.FromCaseNumber("detected-2026-10-15-12-00-0"));
        }

        [Fact]
        public void FromCaseNumber_SuspectedPrefix_ReturnsSuspected()
        {
            Assert.Equal("Suspected", CaseTypeClassifier.FromCaseNumber("suspected-2026-10-15-12-00-1"));
        }

        [Fact]
        public void FromCaseNumber_ManualPrefix_ReturnsManual()
        {
            Assert.Equal("Manual", CaseTypeClassifier.FromCaseNumber("manual-2026-10-15-12-00-2"));
        }

        [Fact]
        public void FromCaseNumber_UnknownPrefix_ReturnsOther()
        {
            Assert.Equal("Other", CaseTypeClassifier.FromCaseNumber("CASE-2026-001"));
        }

        [Fact]
        public void FromCaseNumber_Null_ReturnsOther()
        {
            Assert.Equal("Other", CaseTypeClassifier.FromCaseNumber(null));
        }

        [Fact]
        public void FromCaseNumber_EmptyString_ReturnsOther()
        {
            Assert.Equal("Other", CaseTypeClassifier.FromCaseNumber(string.Empty));
        }

        [Theory]
        [InlineData("DETECTED-2026-10-15-12-00-0")]
        [InlineData("Suspected-2026-10-15-12-00-1")]
        [InlineData("MANUAL-2026-10-15-12-00-2")]
        public void FromCaseNumber_UppercasePrefix_ReturnsOther(string caseNumber)
        {
            // Prefix match is ordinal and case-sensitive; case numbers are always generated in lowercase.
            Assert.Equal("Other", CaseTypeClassifier.FromCaseNumber(caseNumber));
        }

        [Theory]
        [InlineData("detected")]
        [InlineData("detectedX-2026")]
        [InlineData("xdetected-2026-10-15-12-00-0")]
        public void FromCaseNumber_PrefixWithoutExactDashOrAnchored_ReturnsOther(string caseNumber)
        {
            Assert.Equal("Other", CaseTypeClassifier.FromCaseNumber(caseNumber));
        }

        [Fact]
        public void FromCaseNumber_BarePrefixWithDash_ReturnsDetected()
        {
            Assert.Equal("Detected", CaseTypeClassifier.FromCaseNumber("detected-"));
        }
    }
}
