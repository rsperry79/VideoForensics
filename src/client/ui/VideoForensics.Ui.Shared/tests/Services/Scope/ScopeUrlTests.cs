using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Scope;

public class ScopeUrl_Merge_Tests
{
    [Fact]
    public void Merge_WithNoUnrelatedKeys_ReplacesQueryString()
    {
        // Arrange
        string currentUri = "https://example.com/devices";
        string scopeQuery = "devices=abc&from=2026-09-01&to=2026-09-10";

        // Act
        string result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("?", result);
        Assert.Contains("devices=abc", result);
        Assert.Contains("from=2026-09-01", result);
        Assert.Contains("to=2026-09-10", result);
        Assert.DoesNotContain("#", result[..(result.IndexOf("?") + 1)]); // No fragment before query
    }

    [Fact]
    public void Merge_KeepsUnrelatedKeys()
    {
        // Arrange
        string currentUri = "https://example.com/page?tab=raw&filter=status";
        string scopeQuery = "devices=abc";

        // Act
        string result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("tab=raw", result);
        Assert.Contains("filter=status", result);
        Assert.Contains("devices=abc", result);
    }

    [Fact]
    public void Merge_RemovesScopeKeysWhenScopeQueryIsEmpty()
    {
        // Arrange
        string currentUri = "https://example.com/page?devices=abc&from=2026-09-01&tab=raw";
        string scopeQuery = string.Empty;

        // Act
        string result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.DoesNotContain("devices=", result);
        Assert.DoesNotContain("from=", result);
        Assert.Contains("tab=raw", result);
    }

    [Fact]
    public void Merge_PreservesPath()
    {
        // Arrange
        string currentUri = "https://example.com/devices/list?old=query";
        string scopeQuery = "devices=abc&from=2026-09-01";

        // Act
        string result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("/devices/list", result);
    }

    [Fact]
    public void Merge_PreservesFragment()
    {
        // Arrange
        string currentUri = "https://example.com/page?old=query#section1";
        string scopeQuery = "devices=abc";

        // Act
        string result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("#section1", result);
        Assert.Contains("devices=abc", result);
    }

    [Fact]
    public void Merge_ReplacesExistingScopeKeys()
    {
        // Arrange
        string currentUri = "https://example.com/page?devices=oldid&from=2026-01-01&to=2026-01-10&tab=raw";
        string scopeQuery = "devices=newid&from=2026-09-01&to=2026-09-10";

        // Act
        string result = ScopeUrl.Merge(currentUri, scopeQuery);

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
        string currentUri = "/devices?old=query";
        string scopeQuery = "devices=abc&from=2026-09-01";

        // Act
        string result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("/devices", result);
        Assert.Contains("devices=abc", result);
    }

    [Fact]
    public void Merge_HandlesNoExistingQuery()
    {
        // Arrange
        string currentUri = "https://example.com/page";
        string scopeQuery = "devices=abc";

        // Act
        string result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("?devices=abc", result);
    }

    [Fact]
    public void Merge_HandlesEmptyCurrentUri()
    {
        // Arrange
        string currentUri = string.Empty;
        string scopeQuery = "devices=abc";

        // Act
        string result = ScopeUrl.Merge(currentUri, scopeQuery);

        // Assert
        Assert.Contains("devices=abc", result);
    }
}
