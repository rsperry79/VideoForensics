namespace VideoForensics.Ui.Shared.Tests;

using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;
using Syncfusion.Blazor.DropDowns;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

/// <summary>
/// Tests for the release channel card on the update page. The card is shown only to Admin and above; only a
/// SuperAdmin viewer gets the dropdown and Save button, and Admin sees the current channel as read-only text.
/// Viewers below Admin get no card and no channel request. The server enforces the rules; these tests cover
/// presentation only. Also covers the localized relative timestamp for the last-checked time.
/// </summary>
public class UpdateCheckReleaseChannelTests : BunitContext
{
    private readonly Mock<IUpdateCheckService> _updateCheck = new();
    private readonly Mock<IReleaseChannelService> _releaseChannel = new();
    private readonly PairedSessionState _session;

    public UpdateCheckReleaseChannelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddLocalization();

        _updateCheck.Setup(s => s.GetState())
            .Returns(new UpdateCheckState(false, null, "1.0.0", null, null, null));
        Services.AddScoped(_ => _updateCheck.Object);
        Services.AddScoped(_ => _releaseChannel.Object);

        _session = new PairedSessionState(JSInterop.JSRuntime);
        Services.AddScoped(_ => _session);

        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    private IStringLocalizer<UpdateCheck> Localizer => Services.GetRequiredService<IStringLocalizer<UpdateCheck>>();

    private async Task SignInAsync(OperatorRole role)
    {
        await _session.SetAsync("test-token", Guid.NewGuid(), role.ToString());
    }

    private IRenderedComponent<UpdateCheck> RenderPage() => Render<UpdateCheck>();

    private IRenderedComponent<SfDropDownList<UpdateReleaseChannel, KeyValuePair<UpdateReleaseChannel, string>>>? FindDropDown(IRenderedComponent<UpdateCheck> cut)
    {
        var all = cut.FindComponents<SfDropDownList<UpdateReleaseChannel, KeyValuePair<UpdateReleaseChannel, string>>>();
        return all.Count == 0 ? null : all[0];
    }

    private AngleSharp.Dom.IElement? FindSaveButton(IRenderedComponent<UpdateCheck> cut)
    {
        var text = Localizer["SaveChannelButton"].Value;
        return cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == text);
    }

    private void SetupChannel(UpdateReleaseChannel current)
    {
        _releaseChannel.Setup(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
    }

    [Fact]
    public async Task UpdateCheck_SuperAdminViewer_ShowsChannelDropdownAndSaveButton()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.SuperAdmin);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.NotNull(FindDropDown(cut)));
        Assert.NotNull(FindSaveButton(cut));
    }

    [Fact]
    public async Task UpdateCheck_AdminViewer_HidesChannelDropdownAndSaveButton()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.Admin);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ReleaseChannelTitle"], cut.Markup));
        Assert.Null(FindDropDown(cut));
        Assert.Null(FindSaveButton(cut));
    }

    [Fact]
    public async Task UpdateCheck_AdminViewer_ShowsReadOnlyChannelAndNote()
    {
        SetupChannel(UpdateReleaseChannel.Testing);
        await SignInAsync(OperatorRole.Admin);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ChannelTesting"], cut.Markup));
        Assert.Contains(Localizer["ReadOnlyChannelNote"], cut.Markup);
    }

    [Fact]
    public async Task UpdateCheck_SuperAdminSave_CallsSetReleaseChannelWithSelectedChannel()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        _releaseChannel.Setup(s => s.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseChannel.Testing);
        await SignInAsync(OperatorRole.SuperAdmin);

        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.NotNull(FindDropDown(cut)));

        var dropDown = FindDropDown(cut)!;
        await cut.InvokeAsync(() => dropDown.Instance.ValueChanged.InvokeAsync(UpdateReleaseChannel.Testing));

        FindSaveButton(cut)!.Click();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ChannelSavedMessage"], cut.Markup));
        _releaseChannel.Verify(
            s => s.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCheck_ChannelLoadFails_ShowsLocalizedLoadFailedMessage()
    {
        _releaseChannel.Setup(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));
        await SignInAsync(OperatorRole.SuperAdmin);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ChannelLoadFailed", "boom"], cut.Markup));
    }

    [Fact]
    public async Task UpdateCheck_ReadOnlyViewer_HidesChannelCardAndSendsNoChannelRequest()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.ReadOnly);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["CurrentVersionLabel"], cut.Markup));
        Assert.DoesNotContain(Localizer["ReleaseChannelTitle"], cut.Markup);
        _releaseChannel.Verify(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCheck_ReviewViewer_HidesChannelCardAndSendsNoChannelRequest()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.Review);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["CurrentVersionLabel"], cut.Markup));
        Assert.DoesNotContain(Localizer["ReleaseChannelTitle"], cut.Markup);
        _releaseChannel.Verify(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCheck_AdminViewer_ShowsChannelCardAndLoadsChannel()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.Admin);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ReleaseChannelTitle"], cut.Markup));
        _releaseChannel.Verify(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCheck_JustNowTimestamp_RendersLocalizedJustNow()
    {
        _updateCheck.Setup(s => s.GetState())
            .Returns(new UpdateCheckState(false, null, "1.0.0", null, DateTime.UtcNow.AddSeconds(-10), null));
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.SuperAdmin);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["JustNow"], cut.Markup));
    }

    [Fact]
    public async Task UpdateCheck_MinutesAgoTimestamp_RendersLocalizedPlural()
    {
        _updateCheck.Setup(s => s.GetState())
            .Returns(new UpdateCheckState(false, null, "1.0.0", null, DateTime.UtcNow.AddMinutes(-5).AddSeconds(-5), null));
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.SuperAdmin);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["MinutesAgo", 5], cut.Markup));
    }

    [Fact]
    public async Task UpdateCheck_ChannelSaveFails_ShowsLocalizedSaveFailedMessage()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        _releaseChannel.Setup(s => s.SetReleaseChannelAsync(It.IsAny<UpdateReleaseChannel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("denied"));
        await SignInAsync(OperatorRole.SuperAdmin);

        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.NotNull(FindSaveButton(cut)));

        FindSaveButton(cut)!.Click();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ChannelSaveFailed", "denied"], cut.Markup));
    }
}
