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
/// </summary>
public class FlagActionsPanel_Tests : BunitContext
{
    private static readonly DateTime FixedNowUtc = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMediaFlagRepository> _repository = new();
    private readonly Mock<ILogger<FlagActionsPanel>> _logger = new();

    public FlagActionsPanel_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(_repository.Object);
        _ = Services.AddSingleton(_logger.Object);
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
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
}
