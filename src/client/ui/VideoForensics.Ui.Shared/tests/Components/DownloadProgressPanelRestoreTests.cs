using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using System.Reactive.Linq;

using VideoForensics.Client.Ui.Shared.Components;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Resources;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components;

/// <summary>A page opened while a sync runs (or after one) gets the output so far and shows the box without IsActive.</summary>
public class DownloadProgressPanelRestoreTests : BunitContext
{
    /// <summary>Source that replays one snapshot to each new subscriber, as the server-side source does for a late page.</summary>
    private sealed class ReplayOnSubscribeSource : IDownloadProgressSource
    {
        public required DownloadProgressSnapshot Snapshot { get; init; }

        public IObservable<DownloadProgressSnapshot> Progress => Observable.Return(Snapshot);
    }

    public DownloadProgressPanelRestoreTests()
    {
        _ = Services.AddSingleton<IStringLocalizer<SharedResources>>(new TaggingLocalizer());
    }

    [Fact]
    public void Panel_OpenedAfterSyncOutputExists_ShowsTheReplayedLogWithoutIsActive()
    {
        var snapshot = new DownloadProgressSnapshot(
            IsDownloading: false,
            FilesCompleted: 0,
            FilesTotal: 0,
            BytesDownloaded: 0L,
            CurrentFile: string.Empty,
            TotalFilesCompleted: 0,
            TotalFilesMatched: 0,
            TotalBytesDownloaded: 0L,
            ActiveConnections: 0,
            CurrentSpeedMbps: 0.0,
            CurrentDeviceIndex: 0,
            CurrentDeviceTotal: 0,
            CurrentDeviceName: string.Empty,
            PreScanCounts: new Dictionary<string, int>(),
            Activity: ["Ring: sync started", "Ring: 3 event(s) synced"],
            LastError: null,
            RemainingReason: null);
        _ = Services.AddSingleton<IDownloadProgressSource>(new ReplayOnSubscribeSource { Snapshot = snapshot });

        IRenderedComponent<DownloadProgressPanel> cut = Render<DownloadProgressPanel>(p => p.Add(x => x.IsActive, false));

        cut.WaitForAssertion(() => Assert.Contains("Ring: 3 event(s) synced", cut.Markup));
    }
}
