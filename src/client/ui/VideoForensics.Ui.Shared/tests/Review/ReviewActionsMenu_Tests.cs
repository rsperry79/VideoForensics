using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Evidence;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The review page's Actions menu: tag, start a case from the selection, legal hold, integrity check, and export,
/// each gated by the selection and the operator's role, and each failing into a message rather than an exception.
/// </summary>
public class ReviewActionsMenu_Tests : ReviewTestContext
{
    private Guid _clipId;
    private Guid _deviceId;
    private Guid _eventId;

    /// <summary>Puts the cursor on a clip of the primary camera, so the menu has a covering clip and a device.</summary>
    private void SelectClipAtCursor()
    {
        AddDevices(2);
        _deviceId = Devices[0].Id;
        ReviewState.SetCameras([_deviceId]);
        _clipId = Guid.NewGuid();
        DateTime clipStart = FixedNowUtc.AddMinutes(-30);
        TimelineModel.Publish([new ClipWindow(_clipId.ToString("D"), clipStart, 600)], FixedNowUtc.AddHours(-24), FixedNowUtc);
        ReviewState.SetCursor(clipStart.AddSeconds(100));
    }

    /// <summary>Selects an event on the primary camera, without a covering clip.</summary>
    private void SelectEvent()
    {
        AddDevices(2);
        _deviceId = Devices[0].Id;
        ReviewState.SetCameras([_deviceId]);
        _eventId = Guid.NewGuid();
        ReviewState.SelectEvent(_eventId);
    }

    private IRenderedComponent<ReviewActionsMenu> RenderMenu()
    {
        return Render<ReviewActionsMenu>(parameters => parameters.Add(p => p.State, ReviewState));
    }

    private async Task<IRenderedComponent<ReviewActionsMenu>> OpenMenuAsync()
    {
        IRenderedComponent<ReviewActionsMenu> cut = RenderMenu();
        await ClickAsync(cut, "[data-testid='review-actions']");
        return cut;
    }

    private static async Task ClickAsync(IRenderedComponent<ReviewActionsMenu> cut, string selector)
    {
        await cut.InvokeAsync(() => cut.Find(selector).Click());
    }

    private static string? MessageText(IRenderedComponent<ReviewActionsMenu> cut)
    {
        IElement[] messages = cut.FindAll("[data-testid='review-actions-message']").ToArray();
        return messages.Length == 0 ? null : messages[0].TextContent.Trim();
    }

    private void VerifyErrorLogged(string exceptionType, string message)
    {
        ActionsLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(exceptionType, StringComparison.Ordinal) && state.ToString()!.Contains(message, StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReviewActionsMenu_ToggleClick_OpensThePopup()
    {
        // Arrange
        SelectClipAtCursor();
        IRenderedComponent<ReviewActionsMenu> cut = RenderMenu();
        Assert.Empty(cut.FindAll("[data-testid='review-actions-popup']"));

        // Act
        await ClickAsync(cut, "[data-testid='review-actions']");

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-actions-popup']"));
        Assert.Equal("true", cut.Find("[data-testid='review-actions']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task ReviewActionsMenu_TagWithSelectedEvent_KeepsAnEventKindTagPending()
    {
        // Arrange
        SelectClipAtCursor();
        SelectEvent();
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Act
        await ClickAsync(cut, "[data-testid='review-action-tag']");

        // Assert
        PendingTag tag = Assert.Single(Services.GetRequiredService<PendingEvidenceTags>().Items);
        Assert.Equal(CaseItemKind.Event, tag.Kind);
        Assert.Equal(_eventId, tag.TargetId);
        Assert.Equal(_deviceId, tag.DeviceId);
        Assert.Equal(L["TagDefaultReason"].Value, tag.Reason);
        Assert.Equal(L["ResultTagKeptPending"].Value, MessageText(cut));
    }

    [Fact]
    public async Task ReviewActionsMenu_TagWithOnlyAClip_KeepsAMediaKindTagPending()
    {
        // Arrange
        SelectClipAtCursor();
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Act
        await ClickAsync(cut, "[data-testid='review-action-tag']");

        // Assert
        PendingTag tag = Assert.Single(Services.GetRequiredService<PendingEvidenceTags>().Items);
        Assert.Equal(CaseItemKind.Media, tag.Kind);
        Assert.Equal(_clipId, tag.TargetId);
    }

    [Fact]
    public async Task ReviewActionsMenu_TagTwice_SecondTagShowsAlreadyTagged()
    {
        // Arrange
        SelectClipAtCursor();
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();
        await ClickAsync(cut, "[data-testid='review-action-tag']");

        // Act
        await ClickAsync(cut, "[data-testid='review-action-tag']");

        // Assert
        Assert.Single(Services.GetRequiredService<PendingEvidenceTags>().Items);
        Assert.Equal(L["ResultTagAlreadyTagged"].Value, MessageText(cut));
    }

    [Fact]
    public async Task ReviewActionsMenu_TagWithOpenCase_PinsToTheCaseAndShowsPinned()
    {
        // Arrange
        SelectClipAtCursor();
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var pinned = new List<Guid>();
        CaseRepositoryDouble = PendingTagFakes.CreateRepository(openCase, pinnedTargets: pinned).Object;
        _ = await Services.GetRequiredService<CaseState>().ActivateAsync(openCase.Id, CancellationToken.None);
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Act
        await ClickAsync(cut, "[data-testid='review-action-tag']");

        // Assert
        Assert.Equal([_clipId], pinned);
        Assert.Empty(Services.GetRequiredService<PendingEvidenceTags>().Items);
        Assert.Equal(L["ResultTagPinned"].Value, MessageText(cut));
    }

    [Fact]
    public async Task ReviewActionsMenu_TagWithOpenCaseThatAlreadyHoldsIt_ShowsAlreadyTagged()
    {
        // Arrange
        SelectClipAtCursor();
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var existing = new List<CaseItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CaseId = openCase.Id,
                Kind = CaseItemKind.Media,
                MediaItemId = _clipId,
                Reason = PendingTagFakes.Reason,
                AddedBy = "test",
                AddedAtUtc = FixedNowUtc,
            },
        };
        var pinned = new List<Guid>();
        CaseRepositoryDouble = PendingTagFakes.CreateRepository(openCase, existingItems: existing, pinnedTargets: pinned).Object;
        _ = await Services.GetRequiredService<CaseState>().ActivateAsync(openCase.Id, CancellationToken.None);
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Act
        await ClickAsync(cut, "[data-testid='review-action-tag']");

        // Assert
        Assert.Empty(pinned);
        Assert.Equal(L["ResultTagAlreadyTagged"].Value, MessageText(cut));
    }

    [Fact]
    public async Task ReviewActionsMenu_TagWhenThePinFails_ShowsFailedAndKeepsNothing()
    {
        // Arrange
        SelectClipAtCursor();
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        CaseRepositoryDouble = PendingTagFakes.CreateRepository(openCase, failingTargets: new HashSet<Guid> { _clipId }).Object;
        _ = await Services.GetRequiredService<CaseState>().ActivateAsync(openCase.Id, CancellationToken.None);
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Act
        await ClickAsync(cut, "[data-testid='review-action-tag']");

        // Assert
        Assert.Empty(Services.GetRequiredService<PendingEvidenceTags>().Items);
        Assert.Equal(L["ResultTagFailed"].Value, MessageText(cut));
    }

    [Fact]
    public async Task ReviewActionsMenu_StartNewCase_TagsThenSetsScopeAndNavigatesToNewCase()
    {
        // Arrange
        SelectClipAtCursor();
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Act
        await ClickAsync(cut, "[data-testid='review-action-new-case']");

        // Assert
        Assert.Single(Services.GetRequiredService<PendingEvidenceTags>().Items);
        Assert.EndsWith("/cases/new", CurrentUri, StringComparison.Ordinal);
        Assert.Equal([_deviceId], ScopeState.Current.DeviceIds);
        Assert.Equal(ReviewState.FromUtc, ScopeState.Current.FromUtc);
        Assert.Equal(ReviewState.ToUtc, ScopeState.Current.ToUtc);
    }

    [Fact]
    public void ReviewActionsMenu_NoSelection_DisablesTagAndNewCaseWithAReason()
    {
        // Arrange
        AddDevices(1);
        ReviewState.SetCameras([Devices[0].Id]);

        // Act
        IRenderedComponent<ReviewActionsMenu> cut = RenderMenu();
        cut.Find("[data-testid='review-actions']").Click();

        // Assert
        Assert.True(cut.Find("[data-testid='review-action-tag']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='review-action-new-case']").HasAttribute("disabled"));
        Assert.Equal(L["ReasonNoSelection"].Value, cut.Find("[data-testid='review-action-reason-tag']").TextContent.Trim());
    }

    [Fact]
    public async Task ReviewActionsMenu_NoClipAtCursor_DisablesHoldVerifyAndExportWithAReason()
    {
        // Arrange
        SelectClipAtCursor();
        ReviewState.SetCursor(null);

        // Act
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Assert
        Assert.True(cut.Find("[data-testid='review-action-hold']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='review-action-verify']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='review-action-export']").HasAttribute("disabled"));
        Assert.Equal(L["ReasonNoClipAtCursor"].Value, cut.Find("[data-testid='review-action-reason-hold']").TextContent.Trim());
        Assert.Equal(L["ReasonNoClipAtCursor"].Value, cut.Find("[data-testid='review-action-reason-verify']").TextContent.Trim());
        Assert.Equal(L["ReasonNoClipAtCursor"].Value, cut.Find("[data-testid='review-action-reason-export']").TextContent.Trim());
    }

    [Fact]
    public async Task ReviewActionsMenu_ReviewRole_DisablesHoldAndExportButKeepsTagAndVerify()
    {
        // Arrange
        SelectClipAtCursor();

        // Act
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Assert
        Assert.True(cut.Find("[data-testid='review-action-hold']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='review-action-export']").HasAttribute("disabled"));
        Assert.Equal(L["ReasonRequiresAdmin"].Value, cut.Find("[data-testid='review-action-reason-hold']").TextContent.Trim());
        Assert.Equal(L["ReasonRequiresSuperAdmin"].Value, cut.Find("[data-testid='review-action-reason-export']").TextContent.Trim());
        Assert.False(cut.Find("[data-testid='review-action-tag']").HasAttribute("disabled"));
        Assert.False(cut.Find("[data-testid='review-action-verify']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ReviewActionsMenu_PlaceHold_RequiresAReasonAndCallsPlaceWithOperatorIdentity()
    {
        // Arrange
        Guid operatorId = await SignInOperatorAsAsync("Admin");
        SelectClipAtCursor();
        LegalHoldRepositoryMock
            .Setup(r => r.PlaceAsync(_clipId, "Active investigation", operatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegalHold { Id = Guid.NewGuid(), MediaItemId = _clipId, Reason = "Active investigation", CreatedBy = operatorId.ToString() });
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();
        await ClickAsync(cut, "[data-testid='review-action-hold']");

        // Assert: confirm stays disabled until a reason is typed
        Assert.True(cut.Find("[data-testid='review-hold-confirm']").HasAttribute("disabled"));

        // Act
        cut.Find("[data-testid='review-hold-reason']").Change("Active investigation");
        await ClickAsync(cut, "[data-testid='review-hold-confirm']");

        // Assert
        LegalHoldRepositoryMock.Verify(
            r => r.PlaceAsync(_clipId, "Active investigation", operatorId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(L["HoldPlaced"].Value, MessageText(cut));
        Assert.Empty(cut.FindAll("[data-testid='review-hold-reason']"));
        Assert.Single(cut.FindAll("[data-testid='review-action-release']"));
    }

    [Fact]
    public async Task ReviewActionsMenu_ReleaseHold_ActiveHold_CallsReleaseWithReason()
    {
        // Arrange
        Guid operatorId = await SignInOperatorAsAsync("Admin");
        SelectClipAtCursor();
        var activeHold = new LegalHold { Id = Guid.NewGuid(), MediaItemId = _clipId, Reason = "r", CreatedBy = "op" };
        LegalHoldRepositoryMock
            .Setup(m => m.GetActiveByMediaItemIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold> { activeHold });
        LegalHoldRepositoryMock
            .Setup(r => r.ReleaseAsync(activeHold.Id, operatorId.ToString(), "No longer needed", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Act
        await ClickAsync(cut, "[data-testid='review-action-release']");
        cut.Find("[data-testid='review-hold-reason']").Change("No longer needed");
        await ClickAsync(cut, "[data-testid='review-hold-confirm']");

        // Assert
        LegalHoldRepositoryMock.Verify(
            r => r.ReleaseAsync(activeHold.Id, operatorId.ToString(), "No longer needed", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(L["HoldReleased"].Value, MessageText(cut));
        Assert.Single(cut.FindAll("[data-testid='review-action-hold']"));
    }

    [Fact]
    public async Task ReviewActionsMenu_CancelHold_ClearsThePromptAndCallsNothing()
    {
        // Arrange
        _ = await SignInOperatorAsAsync("Admin");
        SelectClipAtCursor();
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();
        await ClickAsync(cut, "[data-testid='review-action-hold']");
        cut.Find("[data-testid='review-hold-reason']").Change("Some reason");

        // Act
        await ClickAsync(cut, "[data-testid='review-hold-cancel']");

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-hold-reason']"));
        LegalHoldRepositoryMock.Verify(
            r => r.PlaceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("verified", "VerifyVerified")]
    [InlineData("failed", "VerifyMismatch")]
    [InlineData("missing", "VerifyMissing")]
    public async Task ReviewActionsMenu_VerifyIntegrity_ShowsTheResultForTheClip(string status, string expectedKey)
    {
        // Arrange
        SelectClipAtCursor();
        EvidenceValidationMock
            .Setup(s => s.VerifyLocalIntegrityAsync(_deviceId, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
            .ReturnsAsync(new List<MediaVerificationResult>
            {
                new() { MediaItemId = _clipId, FileName = "clip.mp4", Status = status },
            });
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Act
        await ClickAsync(cut, "[data-testid='review-action-verify']");

        // Assert
        EvidenceValidationMock.Verify(
            s => s.VerifyLocalIntegrityAsync(_deviceId, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()),
            Times.Once);
        Assert.Equal(L[expectedKey].Value, MessageText(cut));
    }

    [Fact]
    public async Task ReviewActionsMenu_VerifyIntegrity_ClipNotInTheRun_ShowsNotChecked()
    {
        // Arrange
        SelectClipAtCursor();
        EvidenceValidationMock
            .Setup(s => s.VerifyLocalIntegrityAsync(_deviceId, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
            .ReturnsAsync(new List<MediaVerificationResult>
            {
                new() { MediaItemId = Guid.NewGuid(), FileName = "other.mp4", Status = "verified" },
            });
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Act
        await ClickAsync(cut, "[data-testid='review-action-verify']");

        // Assert
        Assert.Equal(L["VerifyNotChecked"].Value, MessageText(cut));
    }

    [Fact]
    public async Task ReviewActionsMenu_ExportWithSuperAdmin_LinksToTheExportUrlForTheClip()
    {
        // Arrange
        _ = await SignInOperatorAsAsync("SuperAdmin");
        SelectClipAtCursor();

        // Act
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Assert
        Assert.Equal(EvidenceActions.BuildExportUrl(new[] { _clipId }), cut.Find("a[data-testid='review-action-export']").GetAttribute("href"));
    }

    [Fact]
    public async Task ReviewActionsMenu_PlaceHoldThrows_ShowsFailedMessageAndLogsTypeAndMessage()
    {
        // Arrange
        _ = await SignInOperatorAsAsync("Admin");
        SelectClipAtCursor();
        LegalHoldRepositoryMock
            .Setup(r => r.PlaceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();
        await ClickAsync(cut, "[data-testid='review-action-hold']");
        cut.Find("[data-testid='review-hold-reason']").Change("Active investigation");

        // Act
        await ClickAsync(cut, "[data-testid='review-hold-confirm']");

        // Assert
        Assert.Equal(L["HoldFailed"].Value, MessageText(cut));
        VerifyErrorLogged("InvalidOperationException", "boom");
    }

    [Fact]
    public async Task ReviewActionsMenu_VerifyThrows_ShowsFailedMessageAndLogsTypeAndMessage()
    {
        // Arrange
        SelectClipAtCursor();
        EvidenceValidationMock
            .Setup(s => s.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        IRenderedComponent<ReviewActionsMenu> cut = await OpenMenuAsync();

        // Act
        await ClickAsync(cut, "[data-testid='review-action-verify']");

        // Assert
        Assert.Equal(L["ActionFailed"].Value, MessageText(cut));
        VerifyErrorLogged("InvalidOperationException", "boom");
    }
}
