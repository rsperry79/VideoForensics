namespace VideoForensics.Ui.Shared.Tests.Services.Evidence;

using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Evidence;
using VideoForensics.Ui.Shared.Services.Inspector;

public class EvidenceActions_ResolveOperatorActor_Tests
{
    [Fact]
    public void ResolveOperatorActor_OperatorSignedIn_ReturnsOperatorIdAsString()
    {
        // Shared by the Grid context menu and the Inspector panel so both surfaces record the
        // same chain-of-custody actor identity for legal hold place/release - never diverging,
        // and never falling back to a machine-level identity like Environment.UserName (the old
        // Events page's bug: that recorded the server process's OS account, not the operator).
        var operatorId = Guid.NewGuid();

        var actor = EvidenceActions.ResolveOperatorActor(operatorId);

        Assert.Equal(operatorId.ToString(), actor);
    }

    [Fact]
    public void ResolveOperatorActor_NoOperatorSignedIn_ReturnsNull()
    {
        var actor = EvidenceActions.ResolveOperatorActor(null);

        Assert.Null(actor);
    }
}

public class EvidenceActions_CanPlaceHold_Tests
{
    [Fact]
    public void CanPlaceHold_MediaPresent_NoActiveHold_ReturnsTrue()
    {
        var actions = new EvidenceActionState(Guid.NewGuid(), Guid.NewGuid(), ActiveHoldId: null);

        Assert.True(EvidenceActions.CanPlaceHold(actions));
    }

    [Fact]
    public void CanPlaceHold_MediaPresent_AlreadyOnHold_ReturnsFalse()
    {
        var actions = new EvidenceActionState(Guid.NewGuid(), Guid.NewGuid(), ActiveHoldId: Guid.NewGuid());

        Assert.False(EvidenceActions.CanPlaceHold(actions));
    }

    [Fact]
    public void CanPlaceHold_NoMediaItem_ReturnsFalse()
    {
        var actions = new EvidenceActionState(MediaItemId: null, Guid.NewGuid(), ActiveHoldId: null);

        Assert.False(EvidenceActions.CanPlaceHold(actions));
    }

    [Fact]
    public void CanPlaceHold_NullActions_ReturnsFalse()
    {
        Assert.False(EvidenceActions.CanPlaceHold(null));
    }
}

public class EvidenceActions_CanReleaseHold_Tests
{
    [Fact]
    public void CanReleaseHold_MediaPresent_OnHold_ReturnsTrue()
    {
        var actions = new EvidenceActionState(Guid.NewGuid(), Guid.NewGuid(), ActiveHoldId: Guid.NewGuid());

        Assert.True(EvidenceActions.CanReleaseHold(actions));
    }

    [Fact]
    public void CanReleaseHold_MediaPresent_NoActiveHold_ReturnsFalse()
    {
        var actions = new EvidenceActionState(Guid.NewGuid(), Guid.NewGuid(), ActiveHoldId: null);

        Assert.False(EvidenceActions.CanReleaseHold(actions));
    }

    [Fact]
    public void CanReleaseHold_NoMediaItem_ReturnsFalse()
    {
        var actions = new EvidenceActionState(MediaItemId: null, Guid.NewGuid(), ActiveHoldId: Guid.NewGuid());

        Assert.False(EvidenceActions.CanReleaseHold(actions));
    }
}

public class EvidenceActions_CanVerifyIntegrity_Tests
{
    [Fact]
    public void CanVerifyIntegrity_MediaPresent_ReturnsTrue()
    {
        var actions = new EvidenceActionState(Guid.NewGuid(), Guid.NewGuid(), ActiveHoldId: null);

        Assert.True(EvidenceActions.CanVerifyIntegrity(actions));
    }

    [Fact]
    public void CanVerifyIntegrity_NoMediaItem_ReturnsFalse()
    {
        // Mirrors EventRow's VerifyIntegrityForDeviceAsync guard: no media means nothing to verify,
        // even though the underlying call is scoped to the whole device.
        var actions = new EvidenceActionState(MediaItemId: null, Guid.NewGuid(), ActiveHoldId: null);

        Assert.False(EvidenceActions.CanVerifyIntegrity(actions));
    }
}

public class EvidenceActions_CanExport_Tests
{
    [Fact]
    public void CanExport_MediaPresent_ReturnsTrue()
    {
        var actions = new EvidenceActionState(Guid.NewGuid(), Guid.NewGuid(), ActiveHoldId: null);

        Assert.True(EvidenceActions.CanExport(actions));
    }

    [Fact]
    public void CanExport_NoMediaItem_ReturnsFalse()
    {
        var actions = new EvidenceActionState(MediaItemId: null, Guid.NewGuid(), ActiveHoldId: null);

        Assert.False(EvidenceActions.CanExport(actions));
    }
}

public class EvidenceActions_GetExportMediaItemIds_Tests
{
    [Fact]
    public void GetExportMediaItemIds_ReturnsDistinctMediaIds_SkippingItemsWithoutMedia()
    {
        var mediaId1 = Guid.NewGuid();
        var mediaId2 = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var items = new List<EvidenceItem>
        {
            MakeItem(deviceId, mediaId: mediaId1),
            MakeItem(deviceId, mediaId: mediaId2),
            MakeItem(deviceId, mediaId: null), // event-only, no media
        };

        var ids = EvidenceActions.GetExportMediaItemIds(items);

        Assert.Equal(new[] { mediaId1, mediaId2 }, ids);
    }

    [Fact]
    public void GetExportMediaItemIds_NoItemsHaveMedia_ReturnsEmpty()
    {
        var deviceId = Guid.NewGuid();
        var items = new List<EvidenceItem> { MakeItem(deviceId, mediaId: null) };

        var ids = EvidenceActions.GetExportMediaItemIds(items);

        Assert.Empty(ids);
    }

    private static EvidenceItem MakeItem(Guid deviceId, Guid? mediaId)
    {
        MediaItem? media = mediaId is null
            ? null
            : new MediaItem
            {
                Id = mediaId.Value,
                DeviceId = deviceId,
                FileName = "f.jpg",
                FilePath = "/f.jpg",
                MediaFormat = "image/jpeg",
                FileSizeBytes = 1,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow,
                Sha256Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            };

        return new EvidenceItem
        {
            Key = mediaId is null ? $"event:{Guid.NewGuid()}" : $"media:{mediaId}",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            DeviceName = "Device",
            Event = null,
            Media = media
        };
    }
}

public class EvidenceActions_BuildExportUrl_Tests
{
    [Fact]
    public void BuildExportUrl_SingleId_ProducesExpectedUrl()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var url = EvidenceActions.BuildExportUrl(new[] { id });

        Assert.Equal($"/review/export?ids={id}", url);
    }

    [Fact]
    public void BuildExportUrl_MultipleIds_JoinsWithComma()
    {
        var id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var url = EvidenceActions.BuildExportUrl(new[] { id1, id2 });

        Assert.Equal($"/review/export?ids={Uri.EscapeDataString($"{id1},{id2}")}", url);
    }
}

public class EvidenceActions_DetermineIntegrityStatus_Tests
{
    [Fact]
    public void DetermineIntegrityStatus_NoRecord_ReturnsNotVerified()
    {
        var mediaId = Guid.NewGuid();

        var (text, color) = EvidenceActions.DetermineIntegrityStatus(mediaId, new List<IntegrityRecord>());

        Assert.Equal("Not verified", text);
        Assert.Equal("goldenrod", color);
    }

    [Fact]
    public void DetermineIntegrityStatus_RecordFailed_ReturnsIntegrityFailed()
    {
        var mediaId = Guid.NewGuid();
        var records = new List<IntegrityRecord>
        {
            new() { Id = Guid.NewGuid(), MediaItemId = mediaId, Sha256Hash = "x", VerifiedAtUtc = DateTime.UtcNow, Passed = false, VerifiedBy = "system" }
        };

        var (text, color) = EvidenceActions.DetermineIntegrityStatus(mediaId, records);

        Assert.Equal("Integrity failed", text);
        Assert.Equal("red", color);
    }

    [Fact]
    public void DetermineIntegrityStatus_RecordPassed_ReturnsVerified()
    {
        var mediaId = Guid.NewGuid();
        var records = new List<IntegrityRecord>
        {
            new() { Id = Guid.NewGuid(), MediaItemId = mediaId, Sha256Hash = "x", VerifiedAtUtc = DateTime.UtcNow, Passed = true, VerifiedBy = "system" }
        };

        var (text, color) = EvidenceActions.DetermineIntegrityStatus(mediaId, records);

        Assert.Equal("Verified", text);
        Assert.Equal("green", color);
    }
}
