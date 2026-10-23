namespace VideoForensics.Ui.Shared.Tests;

using VideoForensics.Ui.Shared.Layout;
using Xunit;

/// <summary>
/// Tests for <see cref="NavPathMatcher"/>, the pure helper extracted from
/// MainLayout/MobileLayout's PathMatches so nav-highlight logic for query-string nav items
/// (e.g. the Analyze sub-tabs, "/analyze?analysis=anomalies") can be tested without standing up
/// the full layout components.
/// </summary>
public class NavPathMatcher_Tests
{
    [Theory]
    [InlineData("/cases", null, "/cases", true)]
    [InlineData("/cases/123", null, "/cases", true)]
    [InlineData("/casesx", null, "/cases", false)]
    [InlineData("/evidence", null, "/cases", false)]
    [InlineData("/", null, "/", true)]
    [InlineData("/evidence", null, "/", false)]
    public void PlainPath_MatchesByExactOrPrefix(string currentPath, string? currentQuery, string itemPath, bool expected)
    {
        Assert.Equal(expected, NavPathMatcher.Matches(currentPath, currentQuery, itemPath));
    }

    [Fact]
    public void QueryItemPath_CurrentPathWrong_DoesNotMatch()
    {
        Assert.False(NavPathMatcher.Matches("/evidence", "?analysis=anomalies", "/analyze?analysis=anomalies"));
    }

    [Fact]
    public void QueryItemPath_NoQueryOnCurrentUri_DoesNotMatch()
    {
        // Bug repro: before extraction, PathMatches only ever compared plain paths, so a nav item
        // like "/analyze?analysis=anomalies" could never match anything - not even the base
        // "/analyze" URI with no query at all.
        Assert.False(NavPathMatcher.Matches("/analyze", "", "/analyze?analysis=anomalies"));
    }

    [Fact]
    public void QueryItemPath_ExactQueryMatch_Matches()
    {
        Assert.True(NavPathMatcher.Matches("/analyze", "?analysis=anomalies", "/analyze?analysis=anomalies"));
    }

    [Fact]
    public void QueryItemPath_CurrentQueryHasExtraKeys_StillMatches()
    {
        // The scope rail appends from=/to=/devices= to the URL; that must not stop the Analyze
        // sub-tab from being highlighted while its own "analysis=" key still matches.
        Assert.True(NavPathMatcher.Matches("/analyze", "?analysis=anomalies&from=2026-09-01&devices=abc", "/analyze?analysis=anomalies"));
    }

    [Fact]
    public void QueryItemPath_CurrentQueryHasDifferentAnalysisValue_DoesNotMatch()
    {
        Assert.False(NavPathMatcher.Matches("/analyze", "?analysis=reports&from=2026-09-01", "/analyze?analysis=anomalies"));
    }

    [Fact]
    public void QueryItemPath_CurrentQueryMissingKey_DoesNotMatch()
    {
        Assert.False(NavPathMatcher.Matches("/analyze", "?from=2026-09-01", "/analyze?analysis=anomalies"));
    }

    [Fact]
    public void QueryItemPath_CurrentQueryWithoutLeadingQuestionMark_StillMatches()
    {
        Assert.True(NavPathMatcher.Matches("/analyze", "analysis=anomalies", "/analyze?analysis=anomalies"));
    }

    [Fact]
    public void QueryItemPath_IsCaseInsensitiveOnPathButCaseSensitiveOnQueryValue()
    {
        Assert.True(NavPathMatcher.Matches("/ANALYZE", "?analysis=anomalies", "/analyze?analysis=anomalies"));
        Assert.False(NavPathMatcher.Matches("/analyze", "?analysis=Anomalies", "/analyze?analysis=anomalies"));
    }
}
