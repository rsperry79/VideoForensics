using System.Text.RegularExpressions;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer;

/// <summary>Guards the rule that MediaPlayer is the only component that renders a video element.</summary>
public class MediaPlayerSourceScan_Tests
{
    // A video tag is "<video" followed by whitespace, ">" or "/". This excludes components such as <VideoSomething>.
    private static readonly Regex VideoTag = new(@"<video[\s>/]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string FindClientRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VideoForensics.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "src", "client");
    }

    [Fact]
    public void VideoElement_AnywhereUnderClientSource_OnlyInMediaPlayer()
    {
        // Arrange
        string root = FindClientRoot();
        char sep = Path.DirectorySeparatorChar;
        string[] skipped = [$"{sep}bin{sep}", $"{sep}obj{sep}"];

        // Act
        List<string> offenders = Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .Where(path => !skipped.Any(s => path.Contains(s, StringComparison.OrdinalIgnoreCase)))
            .Where(path => !string.Equals(Path.GetFileName(path), "MediaPlayer.razor", StringComparison.Ordinal))
            .Where(path => VideoTag.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        // Assert
        Assert.True(offenders.Count == 0, "A video element outside MediaPlayer.razor: " + string.Join(", ", offenders));
    }

    [Fact]
    public void VideoElement_MediaPlayerFile_Exists()
    {
        // Arrange
        string root = FindClientRoot();

        // Act
        string[] found = Directory.GetFiles(root, "MediaPlayer.razor", SearchOption.AllDirectories);

        // Assert
        Assert.Contains(found, p => p.Contains("Components", StringComparison.Ordinal) && p.Contains("Viewer", StringComparison.Ordinal));
    }
}
