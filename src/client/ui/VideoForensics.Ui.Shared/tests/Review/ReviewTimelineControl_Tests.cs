using System.Runtime.CompilerServices;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>
/// Guards U-D6: review pages use the one custom timeline control (TimelineStrip), not Syncfusion's SfSlider.
/// Reads the component sources, so a regression fails here even when no page is rendered.
/// </summary>
public class ReviewTimelineControl_Tests
{
    private static string SrcRoot([CallerFilePath] string thisFile = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "src"));
    }

    [Fact]
    public void PlaybackTimeline_Removed_NoSfSliderReference()
    {
        string root = SrcRoot();
        var offenders = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(f => File.ReadAllText(f).Contains("SfSlider", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void FlagReviewPage_Timeline_UsesTimelineStrip()
    {
        string histogram = File.ReadAllText(Path.Combine(SrcRoot(), "Components", "Review", "HistogramTimeline.razor"));

        Assert.Contains("<TimelineStrip", histogram, StringComparison.Ordinal);
    }
}
