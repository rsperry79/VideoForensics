using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using AngleSharp.Dom;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>
/// The flag actions panel: Keep and Dismiss go through the status path for the signed-in operator, Dismiss needs a reason,
/// a compare-and-set miss shows the conflict message and raises no change, and the Review role gates both actions.
/// A clip flag also offers Add to case, which pins the clip to the picked open case.
/// </summary>
public class FlagActionsPanel_Tests : BunitContext
{
    private static readonly DateTime FixedNowUtc = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMediaFlagRepository> _repository = new();
    private readonly Mock<ICaseRepository> _cases = new();
    private readonly Mock<IMediaItemRepository> _media = new();
    private readonly Mock<ISnapshotCaptureRepository> _snapshots = new();
    private readonly Mock<ILogger<FlagActionsPanel>> _logger = new();

    public FlagActionsPanel_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(_repository.Object);
        _ = Services.AddSingleton(_cases.Object);
        _ = Services.AddSingleton(_media.Object);
        _ = Services.AddSingleton(_snapshots.Object);
        _ = Services.AddSingleton(_logger.Object);
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));

        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ForensicCase>());
        _ = _cases.Setup(c => c.ListCasesContainingAsync(CaseItemKind.Media, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ForensicCase>());
        _ = _cases.Setup(c => c.ListItemsAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CaseItem>());
        _ = _media.Setup(m => m.GetByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<MediaItem>());
        _ = _snapshots.Setup(s => s.GetCurrentForDeviceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SnapshotCapture>());
    }

    /// <summary>A clip of the device recorded at the given UTC time.</summary>
    private static MediaItem Clip(Guid deviceId, DateTime recordedAtUtc)
    {
        return new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = "clip.mp4",
            FilePath = "/clip.mp4",
            MediaFormat = "video/mp4",
            Sha256Hash = "hash",
            RecordedAtUtc = recordedAtUtc,
            DownloadedAtUtc = recordedAtUtc,
        };
    }

    /// <summary>A current snapshot capture of the device at the given UTC time.</summary>
    private static SnapshotCapture Snapshot(Guid deviceId, DateTime capturedAtUtc)
    {
        return SnapshotCapture.Rehydrate(Guid.NewGuid(), deviceId, new DateTimeOffset(capturedAtUtc), "fresh", 200, true, "snap.jpg", null);
    }

    /// <summary>Sets the window reads for an RSSI flag: the clips and snapshots the device holds in the padded window.</summary>
    private void SetupWindow(MediaFlag flag, IReadOnlyList<MediaItem> clips, IReadOnlyList<SnapshotCapture> snapshots)
    {
        _ = _media
            .Setup(m => m.GetByDeviceAndDateRangeAsync(flag.DeviceId, flag.WindowStartUtc.AddHours(-1), flag.WindowEndUtc.AddHours(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clips);
        _ = _snapshots
            .Setup(s => s.GetCurrentForDeviceAsync(flag.DeviceId, new DateTimeOffset(flag.WindowStartUtc.AddHours(-1), TimeSpan.Zero), new DateTimeOffset(flag.WindowEndUtc.AddHours(1), TimeSpan.Zero), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);
    }

    private static ForensicCase OpenCase(string title)
    {
        return new ForensicCase
        {
            Id = Guid.NewGuid(),
            CaseNumber = "CASE-" + title,
            Title = title,
            Status = CaseStatus.Open,
            CreatedBy = "test",
        };
    }

    private static MediaFlag ClipFlag()
    {
        return new MediaFlag
        {
            Id = Guid.NewGuid(),
            FlagType = MediaFlagType.BitrateCollapse,
            DeviceId = Guid.NewGuid(),
            MediaItemId = Guid.NewGuid(),
            WindowStartUtc = FixedNowUtc.AddMinutes(-5),
            WindowEndUtc = FixedNowUtc,
            RaisedAtUtc = FixedNowUtc,
            Status = MediaFlagStatus.Unreviewed,
            RowVersion = 1,
        };
    }

    /// <summary>Signs in a paired operator with the given role and returns the operator's id.</summary>
    private async Task<Guid> SignInAsync(string role)
    {
        Guid operatorId = Guid.NewGuid();
        await Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", operatorId, role);
        return operatorId;
    }

    private static MediaFlag Flag()
    {
        return new MediaFlag
        {
            Id = Guid.NewGuid(),
            FlagType = MediaFlagType.RssiDeviation,
            DeviceId = Guid.NewGuid(),
            WindowStartUtc = FixedNowUtc.AddMinutes(-5),
            WindowEndUtc = FixedNowUtc,
            RaisedAtUtc = FixedNowUtc,
            Status = MediaFlagStatus.Unreviewed,
            RowVersion = 1,
        };
    }

    private IRenderedComponent<FlagActionsPanel> RenderPanel(MediaFlag flag, List<MediaFlag> changes)
    {
        return Render<FlagActionsPanel>(p => p
            .Add(c => c.Flag, flag)
            .Add(c => c.OnChanged, EventCallback.Factory.Create<MediaFlag>(this, changes.Add)));
    }

    private async Task ClickAsync(IRenderedComponent<FlagActionsPanel> cut, string selector)
    {
        await cut.InvokeAsync(() => cut.Find(selector).Click());
    }

    [Fact]
    public async Task FlagActionsPanel_Keep_CallsUpdateStatusWithKept()
    {
        // Arrange
        Guid operatorId = await SignInAsync("Review");
        MediaFlag flag = Flag();
        var changes = new List<MediaFlag>();
        _repository
            .Setup(r => r.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, operatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlag { Id = flag.Id, Status = MediaFlagStatus.Kept, RowVersion = 2, ReviewedBy = operatorId.ToString() });
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(flag, changes);

        // Act
        await ClickAsync(cut, "[data-testid='flag-keep']");

        // Assert
        cut.WaitForAssertion(() => Assert.Single(changes));
        _repository.Verify(r => r.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(MediaFlagStatus.Kept, changes[0].Status);
    }

    [Fact]
    public async Task FlagActionsPanel_Dismiss_DisabledUntilReason()
    {
        // Arrange
        Guid operatorId = await SignInAsync("Review");
        MediaFlag flag = Flag();
        var changes = new List<MediaFlag>();
        _repository
            .Setup(r => r.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "Duplicate of earlier flag", operatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlag { Id = flag.Id, Status = MediaFlagStatus.Dismissed, RowVersion = 2, ReviewedBy = operatorId.ToString() });
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(flag, changes);

        // Act and Assert: blank reason keeps Dismiss disabled
        Assert.True(cut.Find("[data-testid='flag-dismiss']").HasAttribute("disabled"));

        // Act: typing a reason enables Dismiss
        cut.Find("#flag-reason").Change("Duplicate of earlier flag");
        Assert.False(cut.Find("[data-testid='flag-dismiss']").HasAttribute("disabled"));

        await ClickAsync(cut, "[data-testid='flag-dismiss']");

        // Assert
        cut.WaitForAssertion(() => Assert.Single(changes));
        _repository.Verify(r => r.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "Duplicate of earlier flag", operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FlagActionsPanel_Conflict_ShowsConflictMessage_AndRaisesNoChange()
    {
        // Arrange
        await SignInAsync("Review");
        MediaFlag flag = Flag();
        var changes = new List<MediaFlag>();
        _repository
            .Setup(r => r.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FlagStatusConflictException(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "someone-else", FixedNowUtc));
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(flag, changes);

        // Act
        await ClickAsync(cut, "[data-testid='flag-keep']");

        // Assert
        IElement alert = cut.WaitForElement("[role='alert']");
        Assert.Equal(TestLocalizer.Create()["FlagActionsConflict"].Value, alert.TextContent.Trim());
        Assert.Empty(changes);
    }

    [Fact]
    public async Task FlagActionsPanel_WithoutReviewRole_DisablesBothButtons()
    {
        // Arrange
        await SignInAsync("ReadOnly");
        var changes = new List<MediaFlag>();
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(Flag(), changes);

        // Act
        cut.Find("#flag-reason").Change("Some reason");

        // Assert
        Assert.True(cut.Find("[data-testid='flag-keep']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='flag-dismiss']").HasAttribute("disabled"));
        Assert.Empty(changes);
    }

    [Fact]
    public async Task FlagActionsPanel_ClipFlag_ShowsAddToCase()
    {
        // Arrange
        await SignInAsync("Review");
        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync([OpenCase("Alpha")]);

        // Act
        IRenderedComponent<FlagActionsPanel> clip = RenderPanel(ClipFlag(), new List<MediaFlag>());
        IRenderedComponent<FlagActionsPanel> rssi = RenderPanel(Flag(), new List<MediaFlag>());

        // Assert: the clip flag offers Add to case; the RSSI flag has no clip, so the section is absent with no text
        Assert.NotEmpty(clip.FindAll("[data-testid='flag-add-to-case']"));
        Assert.Empty(rssi.FindAll("[data-testid='flag-add-to-case']"));
        Assert.Empty(rssi.FindAll("[data-testid='flag-add-to-case-section']"));
        Assert.DoesNotContain(TestLocalizer.Create()["FlagActionsAddToCase"].Value, rssi.Markup);
    }

    [Fact]
    public async Task FlagActionsPanel_AddToCase_PinsTheClip()
    {
        // Arrange
        Guid operatorId = await SignInAsync("Review");
        MediaFlag clip = ClipFlag();
        ForensicCase alpha = OpenCase("Alpha");
        var changes = new List<MediaFlag>();
        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync([alpha]);
        _ = _cases
            .Setup(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clip.MediaItemId!.Value, TestLocalizer.Create()["FlagActionsPinReason"].Value, operatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CaseItem)null!);
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(clip, changes);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='flag-add-to-case-picker']")));

        // Act
        cut.Find("[data-testid='flag-add-to-case-picker']").Change(alpha.Id.ToString());
        await ClickAsync(cut, "[data-testid='flag-add-to-case']");

        // Assert
        cut.WaitForAssertion(() => _cases.Verify(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clip.MediaItemId!.Value, TestLocalizer.Create()["FlagActionsPinReason"].Value, operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once));
        Assert.Empty(changes);
    }

    [Fact]
    public async Task FlagActionsPanel_AlreadyInCase_DisablesAdd()
    {
        // Arrange
        await SignInAsync("Review");
        MediaFlag clip = ClipFlag();
        ForensicCase alpha = OpenCase("Alpha");
        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync([alpha]);
        _ = _cases.Setup(c => c.ListCasesContainingAsync(CaseItemKind.Media, clip.MediaItemId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync([alpha]);
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(clip, new List<MediaFlag>());

        // Act
        cut.WaitForAssertion(() => Assert.Contains(TestLocalizer.Create()["FlagActionsAlreadyIn", alpha.Title].Value, cut.Markup));
        cut.Find("[data-testid='flag-add-to-case-picker']").Change(alpha.Id.ToString());

        // Assert
        Assert.True(cut.Find("[data-testid='flag-add-to-case']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task FlagActionsPanel_AddDuplicate_ShowsDuplicateMessage()
    {
        // Arrange
        await SignInAsync("Review");
        MediaFlag clip = ClipFlag();
        ForensicCase alpha = OpenCase("Alpha");
        bool pinned = false;
        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync([alpha]);
        _ = _cases
            .Setup(c => c.ListCasesContainingAsync(CaseItemKind.Media, clip.MediaItemId!.Value, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<ForensicCase>>(pinned ? [alpha] : []));
        _ = _cases
            .Setup(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clip.MediaItemId!.Value, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => pinned = true)
            .ThrowsAsync(new InvalidOperationException($"Target Media {clip.MediaItemId} is already actively pinned to case {alpha.Id}."));
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(clip, new List<MediaFlag>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='flag-add-to-case-picker']")));
        cut.Find("[data-testid='flag-add-to-case-picker']").Change(alpha.Id.ToString());

        // Act
        await ClickAsync(cut, "[data-testid='flag-add-to-case']");

        // Assert
        IElement alert = cut.WaitForElement("[data-testid='flag-actions-error']");
        Assert.Equal(TestLocalizer.Create()["FlagActionsDuplicate"].Value, alert.TextContent.Trim());
    }

    [Fact]
    public async Task FlagActionsPanel_RssiFlag_ShowsWindowCounts()
    {
        // Arrange
        await SignInAsync("Review");
        MediaFlag flag = Flag();
        SetupWindow(
            flag,
            [Clip(flag.DeviceId, FixedNowUtc.AddMinutes(-30)), Clip(flag.DeviceId, FixedNowUtc.AddMinutes(-20))],
            [Snapshot(flag.DeviceId, FixedNowUtc.AddMinutes(-25))]);

        // Act
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(flag, new List<MediaFlag>());

        // Assert
        cut.WaitForAssertion(() => Assert.Contains(TestLocalizer.Create()["FlagActionsWindowCounts", 2, 1].Value, cut.Markup));
    }

    [Fact]
    public async Task FlagActionsPanel_RssiConfirm_PinsEveryClipAndSnapshot()
    {
        // Arrange
        Guid operatorId = await SignInAsync("Review");
        MediaFlag flag = Flag();
        ForensicCase alpha = OpenCase("Alpha");
        MediaItem clipA = Clip(flag.DeviceId, FixedNowUtc.AddMinutes(-30));
        MediaItem clipB = Clip(flag.DeviceId, FixedNowUtc.AddMinutes(-20));
        SnapshotCapture snap = Snapshot(flag.DeviceId, FixedNowUtc.AddMinutes(-25));
        string reason = TestLocalizer.Create()["FlagActionsPinReasonRssi"].Value;
        var changes = new List<MediaFlag>();
        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync([alpha]);
        _ = _cases.Setup(c => c.GetAsync(alpha.Id, It.IsAny<CancellationToken>())).ReturnsAsync(alpha);
        _ = _cases.Setup(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clipA.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>())).ReturnsAsync((CaseItem)null!);
        _ = _cases.Setup(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clipB.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>())).ReturnsAsync((CaseItem)null!);
        _ = _cases.Setup(c => c.AddItemAsync(alpha.Id, CaseItemKind.Snapshot, snap.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>())).ReturnsAsync((CaseItem)null!);
        SetupWindow(flag, [clipA, clipB], [snap]);
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(flag, changes);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='flag-window-case-picker']")));
        cut.Find("[data-testid='flag-window-case-picker']").Change(alpha.Id.ToString());

        // Act
        await ClickAsync(cut, "[data-testid='flag-add-window']");

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(TestLocalizer.Create()["FlagActionsWindowDone", 2, 1].Value, cut.Find("[data-testid='flag-window-done']").TextContent.Trim()));
        _cases.Verify(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clipA.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _cases.Verify(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clipB.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _cases.Verify(c => c.AddItemAsync(alpha.Id, CaseItemKind.Snapshot, snap.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _media.Verify(m => m.GetByDeviceAndDateRangeAsync(flag.DeviceId, flag.WindowStartUtc.AddHours(-1), flag.WindowEndUtc.AddHours(1), It.IsAny<CancellationToken>()), Times.Once);
        _snapshots.Verify(s => s.GetCurrentForDeviceAsync(flag.DeviceId, new DateTimeOffset(flag.WindowStartUtc.AddHours(-1), TimeSpan.Zero), new DateTimeOffset(flag.WindowEndUtc.AddHours(1), TimeSpan.Zero), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(changes);
    }

    [Fact]
    public async Task FlagActionsPanel_RssiAlreadyPinned_IsSkipped()
    {
        // Arrange
        Guid operatorId = await SignInAsync("Review");
        MediaFlag flag = Flag();
        ForensicCase alpha = OpenCase("Alpha");
        MediaItem clipA = Clip(flag.DeviceId, FixedNowUtc.AddMinutes(-30));
        MediaItem clipB = Clip(flag.DeviceId, FixedNowUtc.AddMinutes(-20));
        SnapshotCapture snap = Snapshot(flag.DeviceId, FixedNowUtc.AddMinutes(-25));
        string reason = TestLocalizer.Create()["FlagActionsPinReasonRssi"].Value;
        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync([alpha]);
        _ = _cases.Setup(c => c.GetAsync(alpha.Id, It.IsAny<CancellationToken>())).ReturnsAsync(alpha);
        _ = _cases.Setup(c => c.ListItemsAsync(alpha.Id, false, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new CaseItem { Id = Guid.NewGuid(), CaseId = alpha.Id, Kind = CaseItemKind.Media, MediaItemId = clipA.Id, Reason = reason, AddedBy = operatorId.ToString(), AddedAtUtc = FixedNowUtc },
        ]);
        _ = _cases.Setup(c => c.AddItemAsync(alpha.Id, It.IsAny<CaseItemKind>(), It.IsAny<Guid>(), reason, operatorId.ToString(), It.IsAny<CancellationToken>())).ReturnsAsync((CaseItem)null!);
        SetupWindow(flag, [clipA, clipB], [snap]);
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(flag, new List<MediaFlag>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='flag-window-case-picker']")));
        cut.Find("[data-testid='flag-window-case-picker']").Change(alpha.Id.ToString());

        // Act
        await ClickAsync(cut, "[data-testid='flag-add-window']");

        // Assert: the pinned clip is skipped with no error; the other clip and the snapshot are added
        cut.WaitForAssertion(() => Assert.Equal(TestLocalizer.Create()["FlagActionsWindowDone", 1, 1].Value, cut.Find("[data-testid='flag-window-done']").TextContent.Trim()));
        _cases.Verify(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clipA.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Never);
        _cases.Verify(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clipB.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _cases.Verify(c => c.AddItemAsync(alpha.Id, CaseItemKind.Snapshot, snap.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(cut.FindAll("[data-testid='flag-actions-error']"));
    }

    [Fact]
    public async Task FlagActionsPanel_RssiItemFails_ShowsPartialMessage()
    {
        // Arrange
        Guid operatorId = await SignInAsync("Review");
        MediaFlag flag = Flag();
        ForensicCase alpha = OpenCase("Alpha");
        MediaItem clipA = Clip(flag.DeviceId, FixedNowUtc.AddMinutes(-30));
        MediaItem clipB = Clip(flag.DeviceId, FixedNowUtc.AddMinutes(-20));
        SnapshotCapture snap = Snapshot(flag.DeviceId, FixedNowUtc.AddMinutes(-25));
        string reason = TestLocalizer.Create()["FlagActionsPinReasonRssi"].Value;
        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync([alpha]);
        _ = _cases.Setup(c => c.GetAsync(alpha.Id, It.IsAny<CancellationToken>())).ReturnsAsync(alpha);
        _ = _cases.Setup(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clipA.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>())).ReturnsAsync((CaseItem)null!);
        _ = _cases
            .Setup(c => c.AddItemAsync(alpha.Id, CaseItemKind.Media, clipB.Id, reason, operatorId.ToString(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Target Media is missing."));
        SetupWindow(flag, [clipA, clipB], [snap]);
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(flag, new List<MediaFlag>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='flag-window-case-picker']")));
        cut.Find("[data-testid='flag-window-case-picker']").Change(alpha.Id.ToString());

        // Act
        await ClickAsync(cut, "[data-testid='flag-add-window']");

        // Assert: the batch stops at the failed clip, so the snapshot is never attempted; 2 of 3 were not added
        IElement alert = cut.WaitForElement("[data-testid='flag-actions-error']");
        Assert.Equal(TestLocalizer.Create()["FlagActionsWindowPartial", 2, 3].Value, alert.TextContent.Trim());
        _cases.Verify(c => c.AddItemAsync(alpha.Id, CaseItemKind.Snapshot, snap.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FlagActionsPanel_CreateCaseOption_ShowsForm()
    {
        // Arrange
        await SignInAsync("Review");
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(ClipFlag(), new List<MediaFlag>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='flag-add-to-case-picker']")));

        // Act
        cut.Find("[data-testid='flag-add-to-case-picker']").Change("create");

        // Assert
        Assert.Empty(cut.FindAll("#flag-new-case-number"));
        Assert.NotEmpty(cut.FindAll("#flag-new-case-title"));
    }

    [Fact]
    public async Task FlagActionsPanel_CreateCase_CreatesThenAddsTheClip()
    {
        // Arrange
        Guid operatorId = await SignInAsync("Review");
        MediaFlag clip = ClipFlag();
        ForensicCase created = new() { Id = Guid.NewGuid(), CaseNumber = "CASE-9", Title = "Bravo", Status = CaseStatus.Open, CreatedBy = operatorId.ToString() };
        var order = new List<string>();
        _ = _cases
            .Setup(c => c.CreateAsync(null, "Bravo", null, (Guid?)operatorId, null, null, It.IsAny<IReadOnlyCollection<Guid>>(), operatorId.ToString(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("create"))
            .ReturnsAsync(created);
        _ = _cases
            .Setup(c => c.AddItemAsync(created.Id, CaseItemKind.Media, clip.MediaItemId!.Value, TestLocalizer.Create()["FlagActionsPinReason"].Value, operatorId.ToString(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("add"))
            .ReturnsAsync((CaseItem)null!);
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(clip, new List<MediaFlag>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='flag-add-to-case-picker']")));
        cut.Find("[data-testid='flag-add-to-case-picker']").Change("create");
        cut.Find("#flag-new-case-title").Change("Bravo");

        // Act
        await ClickAsync(cut, "[data-testid='flag-add-to-case']");

        // Assert: the case is created first, then the clip is added to it, once each
        cut.WaitForAssertion(() => Assert.Equal(new[] { "create", "add" }, order));
        _cases.Verify(c => c.CreateAsync(null, "Bravo", null, (Guid?)operatorId, null, null, It.IsAny<IReadOnlyCollection<Guid>>(), operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _cases.Verify(c => c.AddItemAsync(created.Id, CaseItemKind.Media, clip.MediaItemId!.Value, TestLocalizer.Create()["FlagActionsPinReason"].Value, operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FlagActionsPanel_CreateCase_Failure_ShowsCreateFailedAndAddsNothing()
    {
        // Arrange
        await SignInAsync("Review");
        MediaFlag clip = ClipFlag();
        _ = _cases
            .Setup(c => c.CreateAsync(null, "Bravo", null, It.IsAny<Guid?>(), null, null, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Case create failed in the repository."));
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(clip, new List<MediaFlag>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='flag-add-to-case-picker']")));
        cut.Find("[data-testid='flag-add-to-case-picker']").Change("create");
        cut.Find("#flag-new-case-title").Change("Bravo");

        // Act
        await ClickAsync(cut, "[data-testid='flag-add-to-case']");

        // Assert: the generic create-failed message shows and the clip is not added to any case
        IElement alert = cut.WaitForElement("[data-testid='flag-actions-error']");
        Assert.Equal(TestLocalizer.Create()["FlagActionsCreateFailed"].Value, alert.TextContent.Trim());
        _cases.Verify(c => c.AddItemAsync(It.IsAny<Guid>(), It.IsAny<CaseItemKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FlagActionsPanel_RssiCounts_ExcludeAlreadyPinnedItems()
    {
        // Arrange
        await SignInAsync("Review");
        MediaFlag flag = Flag();
        ForensicCase alpha = OpenCase("Alpha");
        MediaItem clipA = Clip(flag.DeviceId, FixedNowUtc.AddMinutes(-30));
        MediaItem clipB = Clip(flag.DeviceId, FixedNowUtc.AddMinutes(-20));
        SnapshotCapture snap = Snapshot(flag.DeviceId, FixedNowUtc.AddMinutes(-25));
        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync([alpha]);
        _ = _cases.Setup(c => c.ListItemsAsync(alpha.Id, false, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new CaseItem { Id = Guid.NewGuid(), CaseId = alpha.Id, Kind = CaseItemKind.Media, MediaItemId = clipA.Id, Reason = "pinned", AddedBy = "test", AddedAtUtc = FixedNowUtc },
        ]);
        SetupWindow(flag, [clipA, clipB], [snap]);
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(flag, new List<MediaFlag>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='flag-window-case-picker']")));

        // Act
        cut.Find("[data-testid='flag-window-case-picker']").Change(alpha.Id.ToString());

        // Assert: clipA is already in the case, so only clipB and the snapshot will be added
        cut.WaitForAssertion(() => Assert.Equal(TestLocalizer.Create()["FlagActionsWindowCounts", 1, 1].Value, cut.Find("[data-testid='flag-window-counts']").TextContent.Trim()));
    }

    [Fact]
    public async Task FlagActionsPanel_CreateCase_ScopesToFlagDevice()
    {
        // Arrange
        Guid operatorId = await SignInAsync("Review");
        MediaFlag clip = ClipFlag();
        ForensicCase created = new() { Id = Guid.NewGuid(), CaseNumber = "CASE-9", Title = "Bravo", Status = CaseStatus.Open, CreatedBy = operatorId.ToString() };
        _ = _cases
            .Setup(c => c.CreateAsync(null, "Bravo", null, (Guid?)operatorId, null, null, It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Single() == clip.DeviceId), operatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        _ = _cases
            .Setup(c => c.AddItemAsync(created.Id, CaseItemKind.Media, clip.MediaItemId!.Value, It.IsAny<string>(), operatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CaseItem)null!);
        IRenderedComponent<FlagActionsPanel> cut = RenderPanel(clip, new List<MediaFlag>());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='flag-add-to-case-picker']")));
        cut.Find("[data-testid='flag-add-to-case-picker']").Change("create");
        cut.Find("#flag-new-case-title").Change("Bravo");

        // Act
        await ClickAsync(cut, "[data-testid='flag-add-to-case']");

        // Assert: the new case is scoped to the flag's device only
        cut.WaitForAssertion(() => _cases.Verify(c => c.CreateAsync(null, "Bravo", null, (Guid?)operatorId, null, null, It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Single() == clip.DeviceId), operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once));
    }
}
