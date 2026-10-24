using VideoForensics.Client.Ui.Shared.Services.Evidence;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Evidence;

public class SnapshotWindowThinningTests
{
    private static readonly DateTime Start = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Device = Guid.NewGuid();

    private static EvidenceItem Snapshot(Guid device, DateTime at)
    {
        return new()
        {
            Key = Guid.NewGuid().ToString(),
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = at,
            DeviceId = device,
            DeviceName = "Porch",
        };
    }

    private static EvidenceItem Event(Guid device, DateTime at)
    {
        return new()
        {
            Key = Guid.NewGuid().ToString(),
            Kind = EvidenceKind.Event,
            OccurredAtUtc = at,
            DeviceId = device,
            DeviceName = "Porch",
        };
    }

    [Fact]
    public void Apply_TwoSnapshotsInOneWindow_KeepsTheNewest()
    {
        EvidenceItem early = Snapshot(Device, Start.AddMinutes(1));
        EvidenceItem late = Snapshot(Device, Start.AddMinutes(9));

        IReadOnlyList<EvidenceItem> result = SnapshotWindowThinning.Apply(new[] { early, late }, _ => TimeSpan.FromMinutes(15));

        _ = Assert.Single(result);
        Assert.Same(late, result[0]);
    }

    [Fact]
    public void Apply_SnapshotsInDifferentWindows_KeepsOnePerWindow()
    {
        EvidenceItem first = Snapshot(Device, Start.AddMinutes(1));
        EvidenceItem second = Snapshot(Device, Start.AddMinutes(16));

        IReadOnlyList<EvidenceItem> result = SnapshotWindowThinning.Apply(new[] { first, second }, _ => TimeSpan.FromMinutes(15));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Apply_UsesEachDevicesOwnWindow()
    {
        var other = Guid.NewGuid();
        EvidenceItem a = Snapshot(Device, Start.AddMinutes(1));
        EvidenceItem b = Snapshot(Device, Start.AddMinutes(6));
        EvidenceItem c = Snapshot(other, Start.AddMinutes(1));
        EvidenceItem d = Snapshot(other, Start.AddMinutes(6));

        // Device has 5-minute windows (1 and 6 fall in different ones); the other device has 15-minute windows (both fall in one).
        IReadOnlyList<EvidenceItem> result = SnapshotWindowThinning.Apply(new[] { a, b, c, d }, id => id == Device ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(15));

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Apply_SameWindowDifferentDevices_KeepsOnePerDevice()
    {
        var other = Guid.NewGuid();

        IReadOnlyList<EvidenceItem> result = SnapshotWindowThinning.Apply(
            new[] { Snapshot(Device, Start.AddMinutes(1)), Snapshot(other, Start.AddMinutes(2)) },
            _ => TimeSpan.FromMinutes(15));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Apply_EventsAndVideosPassThroughUnchanged()
    {
        EvidenceItem e1 = Event(Device, Start.AddMinutes(1));
        EvidenceItem e2 = Event(Device, Start.AddMinutes(2));

        IReadOnlyList<EvidenceItem> result = SnapshotWindowThinning.Apply(new[] { e1, e2 }, _ => TimeSpan.FromMinutes(15));

        Assert.Equal(2, result.Count);
    }
}
