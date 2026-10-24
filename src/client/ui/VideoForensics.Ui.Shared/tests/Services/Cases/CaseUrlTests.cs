using VideoForensics.Client.Ui.Shared.Services.Cases;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Cases;

public class CaseUrl_Tests
{
    [Fact]
    public void Merge_WithNullCaseId_RemovesCaseKeyFromUrl()
    {
        // Arrange
        string currentUri = "http://example.com/?case=12345&devices=abc&from=2026-09-01";

        // Act
        string result = CaseUrl.Merge(currentUri, null);

        // Assert
        Assert.DoesNotContain("case=", result);
        Assert.Contains("devices=abc", result);
        Assert.Contains("from=2026-09-01", result);
    }

    [Fact]
    public void Merge_WithCaseId_AddsCaseKeyToUrl()
    {
        // Arrange
        string currentUri = "http://example.com/?devices=abc&from=2026-09-01";
        var caseId = Guid.NewGuid();

        // Act
        string result = CaseUrl.Merge(currentUri, caseId);

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
        string currentUri = $"http://example.com/?case={oldCaseId:D}&devices=abc";

        // Act
        string result = CaseUrl.Merge(currentUri, newCaseId);

        // Assert
        Assert.Contains($"case={newCaseId:D}", result);
        Assert.DoesNotContain($"case={oldCaseId:D}", result);
        Assert.Contains("devices=abc", result);
    }

    [Fact]
    public void Merge_PreservesOtherQueryKeys()
    {
        // Arrange
        string currentUri = "http://example.com/?tab=raw&devices=abc&search=test&from=2026-09-01";
        var caseId = Guid.NewGuid();

        // Act
        string result = CaseUrl.Merge(currentUri, caseId);

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
        string currentUri = "http://example.com/";
        var caseId = Guid.NewGuid();

        // Act
        string result = CaseUrl.Merge(currentUri, caseId);

        // Assert
        Assert.Contains($"case={caseId:D}", result);
        Assert.Contains("?", result);
    }

    [Fact]
    public void Read_WithValidCaseId_ReturnsId()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        string uri = $"http://example.com/?case={caseId:D}&devices=abc";

        // Act
        Guid? result = CaseUrl.Read(uri);

        // Assert
        Assert.Equal(caseId, result);
    }

    [Fact]
    public void Read_WithoutCaseKey_ReturnsNull()
    {
        // Arrange
        string uri = "http://example.com/?devices=abc&from=2026-09-01";

        // Act
        Guid? result = CaseUrl.Read(uri);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Read_WithInvalidCaseId_ReturnsNull()
    {
        // Arrange
        string uri = "http://example.com/?case=not-a-guid&devices=abc";

        // Act
        Guid? result = CaseUrl.Read(uri);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Read_WithEmptyCaseValue_ReturnsNull()
    {
        // Arrange
        string uri = "http://example.com/?case=&devices=abc";

        // Act
        Guid? result = CaseUrl.Read(uri);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Read_RoundTrip_WithMergeAndRead()
    {
        // Arrange
        var originalCaseId = Guid.NewGuid();
        string uri = "http://example.com/?devices=abc";

        // Act
        string merged = CaseUrl.Merge(uri, originalCaseId);
        Guid? read = CaseUrl.Read(merged);

        // Assert
        Assert.Equal(originalCaseId, read);
    }

    [Fact]
    public void Read_WithMultipleCaseKeys_UsesFirst()
    {
        // Arrange
        var caseId1 = Guid.NewGuid();
        var caseId2 = Guid.NewGuid();
        string uri = $"http://example.com/?case={caseId1:D}&case={caseId2:D}";

        // Act
        Guid? result = CaseUrl.Read(uri);

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
        string result = CaseUrl.Merge(null, caseId);
        Assert.Contains($"case={caseId:D}", result);
    }
}
