using System;
using System.Linq;

using VideoForensics.Providers.Ring.Catalog;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Common.Tests;

/// <summary>
/// Tests for the Ring device kind to model and generation catalog.
/// </summary>
public class RingModelCatalogTests
{
    private readonly IRingModelCatalog _catalog = new RingModelCatalog();

    [Fact]
    public void Find_CocoaDoorbellV5_ReturnsNamedModelWithoutGeneration()
    {
        RingModelInfo? info = _catalog.Find("cocoa_doorbell_v5");

        Assert.NotNull(info);
        Assert.Equal(RingDeviceFamily.Doorbell, info.Family);
        Assert.Equal("Wired Doorbell 4K Pro", info.ModelName);
        Assert.Null(info.Generation);
    }

    [Fact]
    public void Find_ChimeV4_ReturnsSecondGenChime()
    {
        RingModelInfo? info = _catalog.Find("chime_v4");

        Assert.NotNull(info);
        Assert.Equal(RingDeviceFamily.Chime, info.Family);
        Assert.Equal("Chime (2nd Gen)", info.ModelName);
        Assert.Equal("2nd Gen", info.Generation);
        Assert.False(info.IsWifiBridge);
    }

    [Theory]
    [InlineData("chime_pro")]
    [InlineData("chime_pro_v2")]
    public void Find_ChimePro_IsWifiBridge(string kind)
    {
        RingModelInfo? info = _catalog.Find(kind);

        Assert.NotNull(info);
        Assert.Equal(RingDeviceFamily.Chime, info.Family);
        Assert.True(info.IsWifiBridge);
    }

    [Fact]
    public void Find_PlainChime_IsNotWifiBridge()
    {
        RingModelInfo? info = _catalog.Find("chime_v2");

        Assert.NotNull(info);
        Assert.False(info.IsWifiBridge);
    }

    [Theory]
    [InlineData("doorbot", RingDeviceFamily.Doorbell)]
    [InlineData("doorbell_v4", RingDeviceFamily.Doorbell)]
    [InlineData("stickup_cam_v4", RingDeviceFamily.Camera)]
    [InlineData("hexa_camera_6_v1", RingDeviceFamily.Camera)]
    public void Find_KnownKind_ReturnsFamily(string kind, RingDeviceFamily expected)
    {
        RingModelInfo? info = _catalog.Find(kind);

        Assert.NotNull(info);
        Assert.Equal(expected, info.Family);
    }

    [Theory]
    [InlineData("doorbot", true)]
    [InlineData("stickup_cam_v4", true)]
    [InlineData("chime_v4", false)]
    public void HasCamera_FollowsFamily(string kind, bool expected)
    {
        RingModelInfo? info = _catalog.Find(kind);

        Assert.NotNull(info);
        Assert.Equal(expected, info.HasCamera);
    }

    [Fact]
    public void Find_UnknownKind_ReturnsNull()
    {
        Assert.Null(_catalog.Find("not_a_ring_kind"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Find_BlankKind_ReturnsNull(string? kind)
    {
        Assert.Null(_catalog.Find(kind));
    }

    [Fact]
    public void All_KindsAreUnique()
    {
        var kinds = _catalog.All.Select(m => m.Kind).ToList();

        Assert.Equal(kinds.Count, kinds.Distinct(StringComparer.Ordinal).Count());
    }
}
