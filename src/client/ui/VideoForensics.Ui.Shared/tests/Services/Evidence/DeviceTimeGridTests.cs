using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services.Evidence;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Evidence;

public class DeviceTimeGrid_Build_Tests
{
    private static Device MakeDevice(string name)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = name,
            Name = name,
            Type = "camera"
        };
    }

    private static EvidenceItem MakeItem(Guid deviceId, string deviceName, DateTime occurredAtUtc, EvidenceKind kind)
    {
        return new()
        {
            Key = $"{kind}:{Guid.NewGuid()}",
            Kind = kind,
            OccurredAtUtc = occurredAtUtc,
            DeviceId = deviceId,
            DeviceName = deviceName,
            Event = null,
            Media = null
        };
    }

    [Fact]
    public void Build_OneDayRange_CreatesHourlyBuckets()
    {
        var from = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);
        Device device = MakeDevice("Front Door");

        DeviceTimeGridResult result = DeviceTimeGrid.Build(from, to, new[] { device }, Array.Empty<EvidenceItem>());

        Assert.Equal(TimeSpan.FromHours(1), result.BucketSize);
        Assert.Equal(24, result.BucketStartsUtc.Count);
        _ = Assert.Single(result.Rows);
        Assert.Equal(24, result.Rows[0].Buckets.Count);
    }

    [Fact]
    public void Build_RangeOverSevenDays_SwitchesToMultiHourBuckets()
    {
        var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = from.AddDays(30); // 720 hours, well over the 168-column cap
        Device device = MakeDevice("Front Door");

        DeviceTimeGridResult result = DeviceTimeGrid.Build(from, to, new[] { device }, Array.Empty<EvidenceItem>());

        Assert.True(result.BucketSize > TimeSpan.FromHours(1));
        Assert.True(result.BucketStartsUtc.Count <= DeviceTimeGrid.MaxColumns);
    }

    [Fact]
    public void Build_ToNotAfterFrom_ReturnsEmptyResult()
    {
        var from = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);

        DeviceTimeGridResult result = DeviceTimeGrid.Build(from, from, Array.Empty<Device>(), Array.Empty<EvidenceItem>());

        Assert.Empty(result.BucketStartsUtc);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Build_CountsEventsAndSnapshotsSeparatelyPerBucket()
    {
        var from = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = from.AddHours(3);
        Device device = MakeDevice("Front Door");

        EvidenceItem[] items = new[]
        {
            MakeItem(device.Id, device.Name, from.AddMinutes(10), EvidenceKind.Event),
            MakeItem(device.Id, device.Name, from.AddMinutes(20), EvidenceKind.Event),
            MakeItem(device.Id, device.Name, from.AddMinutes(30), EvidenceKind.Snapshot),
            MakeItem(device.Id, device.Name, from.AddHours(1).AddMinutes(5), EvidenceKind.Video),
        };

        DeviceTimeGridResult result = DeviceTimeGrid.Build(from, to, new[] { device }, items);

        DeviceTimeRow row = result.Rows.Single();
        Assert.Equal(2, row.Buckets[0].EventCount);
        Assert.Equal(1, row.Buckets[0].SnapshotCount);
        Assert.Equal(0, row.Buckets[1].EventCount);
        Assert.Equal(1, row.Buckets[1].SnapshotCount); // Video counts as a snapshot/media marker
        Assert.Equal(0, row.Buckets[2].EventCount);
        Assert.Equal(0, row.Buckets[2].SnapshotCount);
    }

    [Fact]
    public void Build_EmptyBucketSurroundedByActivity_IsMarkedGap()
    {
        var from = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = from.AddHours(5);
        Device device = MakeDevice("Front Door");

        EvidenceItem[] items = new[]
        {
            MakeItem(device.Id, device.Name, from.AddMinutes(5), EvidenceKind.Event), // bucket 0
            MakeItem(device.Id, device.Name, from.AddHours(4).AddMinutes(5), EvidenceKind.Event), // bucket 4
        };

        DeviceTimeGridResult result = DeviceTimeGrid.Build(from, to, new[] { device }, items);

        DeviceTimeRow row = result.Rows.Single();
        Assert.False(row.Buckets[0].IsGap);
        Assert.True(row.Buckets[1].IsGap);
        Assert.True(row.Buckets[2].IsGap);
        Assert.True(row.Buckets[3].IsGap);
        Assert.False(row.Buckets[4].IsGap);
    }

    [Fact]
    public void Build_LeadingAndTrailingEmptyBuckets_AreNotMarkedGap()
    {
        var from = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = from.AddHours(5);
        Device device = MakeDevice("Front Door");

        EvidenceItem[] items = new[]
        {
            MakeItem(device.Id, device.Name, from.AddHours(2).AddMinutes(5), EvidenceKind.Event), // bucket 2 only
        };

        DeviceTimeGridResult result = DeviceTimeGrid.Build(from, to, new[] { device }, items);

        DeviceTimeRow row = result.Rows.Single();
        Assert.False(row.Buckets[0].IsGap); // leading empty
        Assert.False(row.Buckets[1].IsGap); // leading empty
        Assert.False(row.Buckets[2].IsGap); // has activity
        Assert.False(row.Buckets[3].IsGap); // trailing empty
        Assert.False(row.Buckets[4].IsGap); // trailing empty
    }

    [Fact]
    public void Build_DeviceWithNoActivityAtAll_HasNoGaps()
    {
        var from = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = from.AddHours(3);
        Device device = MakeDevice("Quiet Camera");

        DeviceTimeGridResult result = DeviceTimeGrid.Build(from, to, new[] { device }, Array.Empty<EvidenceItem>());

        DeviceTimeRow row = result.Rows.Single();
        Assert.All(row.Buckets, b => Assert.False(b.IsGap));
    }

    [Fact]
    public void Build_MultipleDevices_EachGetsOwnRowWithOwnCounts()
    {
        var from = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = from.AddHours(2);
        Device deviceA = MakeDevice("Front Door");
        Device deviceB = MakeDevice("Back Yard");

        EvidenceItem[] items = new[]
        {
            MakeItem(deviceA.Id, deviceA.Name, from.AddMinutes(5), EvidenceKind.Event),
            MakeItem(deviceB.Id, deviceB.Name, from.AddMinutes(5), EvidenceKind.Snapshot),
            MakeItem(deviceB.Id, deviceB.Name, from.AddMinutes(6), EvidenceKind.Snapshot),
        };

        DeviceTimeGridResult result = DeviceTimeGrid.Build(from, to, new[] { deviceA, deviceB }, items);

        Assert.Equal(2, result.Rows.Count);
        DeviceTimeRow rowA = result.Rows.Single(r => r.DeviceId == deviceA.Id);
        DeviceTimeRow rowB = result.Rows.Single(r => r.DeviceId == deviceB.Id);
        Assert.Equal(1, rowA.Buckets[0].EventCount);
        Assert.Equal(0, rowA.Buckets[0].SnapshotCount);
        Assert.Equal(0, rowB.Buckets[0].EventCount);
        Assert.Equal(2, rowB.Buckets[0].SnapshotCount);
    }

    [Fact]
    public void Build_ItemOutsideDeviceList_IsIgnored()
    {
        var from = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = from.AddHours(1);
        Device device = MakeDevice("Front Door");
        var strangerId = Guid.NewGuid();

        EvidenceItem[] items = new[]
        {
            MakeItem(strangerId, "Stranger", from.AddMinutes(5), EvidenceKind.Event),
        };

        DeviceTimeGridResult result = DeviceTimeGrid.Build(from, to, new[] { device }, items);

        DeviceTimeRow row = result.Rows.Single();
        Assert.Equal(0, row.Buckets[0].EventCount);
    }
}
