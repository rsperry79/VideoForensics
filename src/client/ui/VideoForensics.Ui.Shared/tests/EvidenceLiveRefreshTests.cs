
using System.Reactive.Subjects;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>Source double whose changes the test pushes directly, standing in for the realtime store.</summary>
public sealed class EvidenceChangedSourceDouble : IEvidenceChangedSource
{
    private readonly Subject<EvidenceChangedDto> _changes;

    public EvidenceChangedSourceDouble(Subject<EvidenceChangedDto> changes)
    {
        _changes = changes;
    }

    public IObservable<EvidenceChangedDto> EvidenceChanged => _changes;
}

/// <summary>The Evidence page reloads itself when the server reports a change, instead of waiting for Refresh.</summary>
public class Evidence_LiveRefresh_Tests : EvidencePageTestBase
{
    private int LoadCount()
    {
        return MediaItemRepositoryMock.Invocations.Count(i => i.Method.Name == nameof(IMediaItemRepository.GetByDeviceAndDateRangeAsync));
    }

    /// <summary>
    /// The page reloads from a debounce timer, on a thread-pool thread that bUnit's WaitForAssertion does not pump, so the
    /// tests poll for the reload instead. Production uses the same InvokeAsync path the thumbnail callback uses.
    /// </summary>
    private async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    [Fact]
    public async Task EvidenceChanged_ReloadsTheEvidence()
    {
        _ = RenderEvidencePage();
        int before = LoadCount();

        EvidenceChanges.OnNext(new EvidenceChangedDto(DeviceId));

        await WaitUntilAsync(() => LoadCount() > before);
        Assert.True(LoadCount() > before);
    }

    /// <summary>A burst of changes (one sync writes many rows) must produce one reload, not one per row.</summary>
    [Fact]
    public async Task EvidenceChangedBurst_ReloadsOnce()
    {
        _ = RenderEvidencePage();
        int before = LoadCount();

        for (int i = 0; i < 5; i++)
        {
            EvidenceChanges.OnNext(new EvidenceChangedDto(DeviceId));
        }

        await WaitUntilAsync(() => LoadCount() > before);
        await Task.Delay(2000);
        Assert.Equal(before + 1, LoadCount());
    }
}
