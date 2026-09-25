namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Ui.Shared.Services.Scope;

public class ScopeUrl_Merge_Tests
{
    [Fact]
    public void Merge_WithNoUnrelatedKeys_ReplacesQueryString()
    {
        // Arrange
        var currentUri = "https://example.com/devices";
        var scopeQuery = "devices=abc&from=2026-09-01&to=2026-09-10";

        // Act
        var result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("?", result);
        Assert.Contains("devices=abc", result);
        Assert.Contains("from=2026-09-01", result);
        Assert.Contains("to=2026-09-10", result);
        Assert.DoesNotContain("#", result.Substring(0, result.IndexOf("?") + 1)); // No fragment before query
    }

    [Fact]
    public void Merge_KeepsUnrelatedKeys()
    {
        // Arrange
        var currentUri = "https://example.com/page?tab=raw&filter=status";
        var scopeQuery = "devices=abc";

        // Act
        var result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("tab=raw", result);
        Assert.Contains("filter=status", result);
        Assert.Contains("devices=abc", result);
    }

    [Fact]
    public void Merge_RemovesScopeKeysWhenScopeQueryIsEmpty()
    {
        // Arrange
        var currentUri = "https://example.com/page?devices=abc&from=2026-09-01&tab=raw";
        var scopeQuery = "";

        // Act
        var result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.DoesNotContain("devices=", result);
        Assert.DoesNotContain("from=", result);
        Assert.Contains("tab=raw", result);
    }

    [Fact]
    public void Merge_PreservesPath()
    {
        // Arrange
        var currentUri = "https://example.com/devices/list?old=query";
        var scopeQuery = "devices=abc&from=2026-09-01";

        // Act
        var result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("/devices/list", result);
    }

    [Fact]
    public void Merge_PreservesFragment()
    {
        // Arrange
        var currentUri = "https://example.com/page?old=query#section1";
        var scopeQuery = "devices=abc";

        // Act
        var result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("#section1", result);
        Assert.Contains("devices=abc", result);
    }

    [Fact]
    public void Merge_ReplacesExistingScopeKeys()
    {
        // Arrange
        var currentUri = "https://example.com/page?devices=oldid&from=2026-01-01&to=2026-01-10&tab=raw";
        var scopeQuery = "devices=newid&from=2026-09-01&to=2026-09-10";

        // Act
        var result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.DoesNotContain("devices=oldid", result);
        Assert.DoesNotContain("from=2026-01-01", result);
        Assert.Contains("devices=newid", result);
        Assert.Contains("from=2026-09-01", result);
        Assert.Contains("tab=raw", result);
    }

    [Fact]
    public void Merge_HandlesRelativeUri()
    {
        // Arrange
        var currentUri = "/devices?old=query";
        var scopeQuery = "devices=abc&from=2026-09-01";

        // Act
        var result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("/devices", result);
        Assert.Contains("devices=abc", result);
    }

    [Fact]
    public void Merge_HandlesNoExistingQuery()
    {
        // Arrange
        var currentUri = "https://example.com/page";
        var scopeQuery = "devices=abc";

        // Act
        var result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("?devices=abc", result);
    }


    [Fact]
    public void Merge_HandlesEmptyCurrentUri()
    {
        // Arrange
        var currentUri = "";
        var scopeQuery = "devices=abc";

        // Act
        var result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("devices=abc", result);
    }
}
