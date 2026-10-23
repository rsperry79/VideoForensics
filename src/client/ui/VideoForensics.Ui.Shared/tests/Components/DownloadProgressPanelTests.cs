namespace VideoForensics.Ui.Shared.Tests.Components;

using System.Reactive.Subjects;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using VideoForensics.Ui.Shared.Components;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Resources;
using Xunit;

public class DownloadProgressPanelTests : BunitContext
{
    /// <summary>Source whose emissions the test drives directly, and which exposes its live observer count.</summary>
    private sealed class FakeProgressSource : IDownloadProgressSource
    {
        public Subject<DownloadProgressSnapshot> Subject { get; } = new();

        public IObservable<DownloadProgressSnapshot> Progress => Subject;
    }

    private readonly FakeProgressSource _source = new();

    public DownloadProgressPanelTests()
    {
        Services.AddSingleton<IDownloadProgressSource>(_source);
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new TaggingLocalizer());
    }

    private static DownloadProgressSnapshot BuildSnapshot(int completed = 1, int total = 4, IReadOnlyList<string>? activity = null)
    {
        return new DownloadProgressSnapshot(
            IsDownloading: true,
            FilesCompleted: completed,
            FilesTotal: total,
            BytesDownloaded: 2048L,
            CurrentFile: "a.mp4",
            TotalFilesCompleted: completed,
            TotalFilesMatched: total,
            TotalBytesDownloaded: 2048L,
            ActiveConnections: 1,
            CurrentSpeedMbps: 0.5,
            CurrentDeviceIndex: 1,
            CurrentDeviceTotal: 3,
            CurrentDeviceName: "Garage",
            PreScanCounts: new Dictionary<string, int>(),
            Activity: activity ?? [],
            LastError: null,
            RemainingReason: null);
    }

    [Fact]
    public void DownloadProgressPanel_SnapshotEmitted_RendersFileCounts()
    {
        IRenderedComponent<DownloadProgressPanel> cut = Render<DownloadProgressPanel>(p => p.Add(c => c.IsActive, true));

        _source.Subject.OnNext(BuildSnapshot(completed: 2, total: 5));

        cut.WaitForAssertion(() => Assert.Contains("DownloadProgressFiles|2|5", cut.Markup));
    }

    [Fact]
    public void DownloadProgressPanel_ActivityFromEachEmission_AppendsEachLineOnce()
    {
        IRenderedComponent<DownloadProgressPanel> cut = Render<DownloadProgressPanel>(p => p.Add(c => c.IsActive, true));

        _source.Subject.OnNext(BuildSnapshot(activity: ["first line"]));
        _source.Subject.OnNext(BuildSnapshot(completed: 2, activity: ["second line"]));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, CountOccurrences(cut.Markup, "first line"));
            Assert.Equal(1, CountOccurrences(cut.Markup, "second line"));
        });
    }

    [Fact]
    public void DownloadProgressPanel_Dispose_UnsubscribesFromSource()
    {
        _ = Render<DownloadProgressPanel>(p => p.Add(c => c.IsActive, true));
        Assert.True(_source.Subject.HasObservers);

        Dispose();

        Assert.False(_source.Subject.HasObservers);
    }

    [Fact]
    public void DownloadProgressPanel_InactiveAndNoEmission_RendersNoPanel()
    {
        IRenderedComponent<DownloadProgressPanel> cut = Render<DownloadProgressPanel>(p => p.Add(c => c.IsActive, false));

        Assert.DoesNotContain("download-progress-panel", cut.Markup);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
