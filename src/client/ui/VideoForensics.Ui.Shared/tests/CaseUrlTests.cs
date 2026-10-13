namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Ui.Shared.Services.Cases;

public class CaseUrl_Tests
{
    [Fact]
    public void Merge_WithNullCaseId_RemovesCaseKeyFromUrl()
    {
        // Arrange
        var currentUri = "http://example.com/?case=12345&devices=abc&from=2026-09-01";

        // Act
        var result = CaseUrl.Merge(currentUri, null);

        // Assert
        Assert.DoesNotContain("case=", result);
        Assert.Contains("devices=abc", result);
        Assert.Contains("from=2026-09-01", result);
    }

    [Fact]
    public void Merge_WithCaseId_AddsCaseKeyToUrl()
    {
        // Arrange
        var currentUri = "http://example.com/?devices=abc&from=2026-09-01";
        var caseId = Guid.NewGuid();

        // Act
        var result = CaseUrl.Merge(currentUri, caseId);

        // Assert
        Assert.Contains($"case={caseId:D}", result);
        Assert.Contains("devices=abc", result);
        Assert.Contains("from=2026-09-01", result);
    }

    [Fact]
    public void Merge_ReplacesExistingCaseId()
    {
        // Arrange
        var oldCaseId = Guid.NewGuid();
        var newCaseId = Guid.NewGuid();
        var currentUri = $"http://example.com/?case={oldCaseId:D}&devices=abc";

        // Act
        var result = CaseUrl.Merge(currentUri, newCaseId);

        // Assert
        Assert.Contains($"case={newCaseId:D}", result);
        Assert.DoesNotContain($"case={oldCaseId:D}", result);
        Assert.Contains("devices=abc", result);
    }

    [Fact]
    public void Merge_PreservesOtherQueryKeys()
    {
        // Arrange
        var currentUri = "http://example.com/?tab=raw&devices=abc&search=test&from=2026-09-01";
        var caseId = Guid.NewGuid();

        // Act
        var result = CaseUrl.Merge(currentUri, caseId);

        // Assert
        Assert.Contains($"case={caseId:D}", result);
        Assert.Contains("tab=raw", result);
        Assert.Contains("devices=abc", result);
        Assert.Contains("search=test", result);
        Assert.Contains("from=2026-09-01", result);
    }

    [Fact]
    public void Merge_WithoutQueryString_AddsCase()
    {
        // Arrange
        var currentUri = "http://example.com/";
        var caseId = Guid.NewGuid();

        // Act
        var result = CaseUrl.Merge(currentUri, caseId);

        // Assert
        Assert.Contains($"case={caseId:D}", result);
        Assert.Contains("?", result);
    }

    [Fact]
    public void Read_WithValidCaseId_ReturnsId()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var uri = $"http://example.com/?case={caseId:D}&devices=abc";

        // Act
        var result = CaseUrl.Read(uri);

        // Assert
        Assert.Equal(caseId, result);
    }

    [Fact]
    public void Read_WithoutCaseKey_ReturnsNull()
    {
        // Arrange
        var uri = "http://example.com/?devices=abc&from=2026-09-01";

        // Act
        var result = CaseUrl.Read(uri);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Read_WithInvalidCaseId_ReturnsNull()
    {
        // Arrange
        var uri = "http://example.com/?case=not-a-guid&devices=abc";

        // Act
        var result = CaseUrl.Read(uri);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Read_WithEmptyCaseValue_ReturnsNull()
    {
        // Arrange
        var uri = "http://example.com/?case=&devices=abc";

        // Act
        var result = CaseUrl.Read(uri);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Read_RoundTrip_WithMergeAndRead()
    {
        // Arrange
        var originalCaseId = Guid.NewGuid();
        var uri = "http://example.com/?devices=abc";

        // Act
        var merged = CaseUrl.Merge(uri, originalCaseId);
        var read = CaseUrl.Read(merged);

        // Assert
        Assert.Equal(originalCaseId, read);
    }

    [Fact]
    public void Read_WithMultipleCaseKeys_UsesFirst()
    {
        // Arrange
        var caseId1 = Guid.NewGuid();
        var caseId2 = Guid.NewGuid();
        var uri = $"http://example.com/?case={caseId1:D}&case={caseId2:D}";

        // Act
        var result = CaseUrl.Read(uri);

        // Assert
        // Should parse the first occurrence
        Assert.Equal(caseId1, result);
    }

    [Fact]
    public void Merge_WithNullUri_HandlesGracefully()
    {
        // Arrange
        var caseId = Guid.NewGuid();

        // Act & Assert - should not throw
        var result = CaseUrl.Merge(null, caseId);
        Assert.Contains($"case={caseId:D}", result);
    }
}
