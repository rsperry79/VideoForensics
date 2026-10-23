namespace VideoForensics.Ui.Shared.Tests;

using System.Net.Http;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

/// <summary>
/// Tests for the result message shown after a manual "Check now" on the update page.
/// </summary>
public class UpdateCheckResultMessageTests : BunitContext
{
    private readonly Mock<IUpdateCheckService> _updateCheck = new();
    private readonly Mock<IReleaseChannelService> _releaseChannel = new();
    private UpdateCheckState _state = new(false, null, "1.0.0", null, null, null);

    public UpdateCheckResultMessageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddLocalization();

        _updateCheck.Setup(s => s.GetState()).Returns(() => _state);
        Services.AddScoped(_ => _updateCheck.Object);
        Services.AddScoped(_ => _releaseChannel.Object);
        var session = new PairedSessionState(JSInterop.JSRuntime);
        Services.AddScoped(_ => session);

        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    private IStringLocalizer<UpdateCheck> Localizer => Services.GetRequiredService<IStringLocalizer<UpdateCheck>>();

    private void ClickCheckNow(IRenderedComponent<UpdateCheck> cut)
    {
        cut.WaitForAssertion(() => cut.Find("button.e-primary"));
        cut.Find("button.e-primary").Click();
    }

    [Fact]
    public void UpdateCheck_CheckNowFindsUpdate_ShowsUpdateAvailableMessageWithLatestVersion()
    {
        _updateCheck.Setup(s => s.TriggerCheckNowAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _state = new UpdateCheckState(true, "2.5.0", "1.0.0", null, DateTime.UtcNow, null))
            .Returns(Task.CompletedTask);
        var cut = Render<UpdateCheck>();

        ClickCheckNow(cut);

        cut.WaitForAssertion(() => Assert.Contains(Localizer["CheckResultUpdateAvailable", "2.5.0"].Value, cut.Markup));
        Assert.DoesNotContain(Localizer["CheckResultUpToDate", "1.0.0"].Value, cut.Markup);
    }

    [Fact]
    public void UpdateCheck_CheckNowNoUpdate_ShowsUpToDateMessageWithCurrentVersion()
    {
        _updateCheck.Setup(s => s.TriggerCheckNowAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _state = new UpdateCheckState(false, null, "1.0.0", null, DateTime.UtcNow, null))
            .Returns(Task.CompletedTask);
        var cut = Render<UpdateCheck>();

        ClickCheckNow(cut);

        cut.WaitForAssertion(() => Assert.Contains(Localizer["CheckResultUpToDate", "1.0.0"].Value, cut.Markup));
    }

    [Fact]
    public void UpdateCheck_CheckNowReturnsErrorState_ShowsNeitherResultMessage()
    {
        _updateCheck.Setup(s => s.TriggerCheckNowAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _state = new UpdateCheckState(false, null, "1.0.0", null, DateTime.UtcNow, "rate limited"))
            .Returns(Task.CompletedTask);
        var cut = Render<UpdateCheck>();

        ClickCheckNow(cut);

        cut.WaitForAssertion(() => Assert.Contains("rate limited", cut.Markup));
        Assert.DoesNotContain(Localizer["CheckResultUpToDate", "1.0.0"].Value, cut.Markup);
        Assert.DoesNotContain(Localizer["CheckResultUpdateAvailable", "2.5.0"].Value, cut.Markup);
    }

    [Fact]
    public void UpdateCheck_InitialRender_ShowsNeitherResultMessage()
    {
        _state = new UpdateCheckState(true, "2.5.0", "1.0.0", null, DateTime.UtcNow, null);
        var cut = Render<UpdateCheck>();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["CurrentVersionLabel"].Value, cut.Markup));
        Assert.DoesNotContain(Localizer["CheckResultUpdateAvailable", "2.5.0"].Value, cut.Markup);
        Assert.DoesNotContain(Localizer["CheckResultUpToDate", "1.0.0"].Value, cut.Markup);
    }

    [Fact]
    public void UpdateCheck_CheckNowThrows_ShowsCheckFailedAndNoResultMessage()
    {
        _updateCheck.Setup(s => s.TriggerCheckNowAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));
        var cut = Render<UpdateCheck>();

        ClickCheckNow(cut);

        cut.WaitForAssertion(() => Assert.Contains(Localizer["CheckFailed", "boom"].Value, cut.Markup));
        Assert.DoesNotContain(Localizer["CheckResultUpToDate", "1.0.0"].Value, cut.Markup);
        Assert.DoesNotContain(Localizer["CheckResultUpdateAvailable", "2.5.0"].Value, cut.Markup);
    }
}
