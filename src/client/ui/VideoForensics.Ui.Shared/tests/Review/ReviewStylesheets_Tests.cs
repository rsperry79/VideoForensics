using System.Text.RegularExpressions;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>Guards for every Ui.Shared scoped stylesheet, not only the review ones.</summary>
public class ReviewStylesheets_Tests
{
    [Fact]
    public void ReviewStylesheets_DeepRule_HasOwnedElementPrefix()
    {
        // Arrange: a bare "::deep .x" rule has no owning element, so Blazor never matches it and the rule is dead
        string root = RepoFile.UiSharedSourceRoot();
        var bare = new Regex(@"^\s*::deep", RegexOptions.Multiline);

        // Act
        List<string> offenders = Directory.EnumerateFiles(root, "*.razor.css", SearchOption.AllDirectories)
            .Where(file => bare.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(root, file))
            .ToList();

        // Assert
        Assert.True(offenders.Count == 0, "Scoped rules must start with an owned element before ::deep: " + string.Join(", ", offenders));
    }
}
