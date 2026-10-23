using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using Moq;

using Syncfusion.Blazor.DropDowns;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Tests for the release channel card on the update page. The card is shown only to Admin and above; only a
/// SuperAdmin viewer gets the dropdown, and Admin sees the current channel as read-only text.
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
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddLocalization();

        _ = _updateCheck.Setup(s => s.GetState())
            .Returns(new UpdateCheckState(false, null, "1.0.0", null, null, null));
        _ = Services.AddScoped(_ => _updateCheck.Object);
        _ = Services.AddScoped(_ => _releaseChannel.Object);

        _session = new PairedSessionState(JSInterop.JSRuntime);
        _ = Services.AddScoped(_ => _session);

        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    private IStringLocalizer<UpdateCheck> Localizer => Services.GetRequiredService<IStringLocalizer<UpdateCheck>>();

    private async Task SignInAsync(OperatorRole role)
    {
        await _session.SetAsync("test-token", Guid.NewGuid(), role.ToString());
    }

    private IRenderedComponent<UpdateCheck> RenderPage()
    {
        return Render<UpdateCheck>();
    }

    private IRenderedComponent<SfDropDownList<UpdateReleaseChannel, KeyValuePair<UpdateReleaseChannel, string>>>? FindDropDown(IRenderedComponent<UpdateCheck> cut)
    {
        IReadOnlyList<IRenderedComponent<SfDropDownList<UpdateReleaseChannel, KeyValuePair<UpdateReleaseChannel, string>>>> all = cut.FindComponents<SfDropDownList<UpdateReleaseChannel, KeyValuePair<UpdateReleaseChannel, string>>>();
        return all.Count == 0 ? null : all[0];
    }

    private void SetupChannel(UpdateReleaseChannel current)
    {
        _ = _releaseChannel.Setup(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
    }

    [Fact]
    public async Task UpdateCheck_SuperAdminViewer_ShowsChannelDropdown()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.SuperAdmin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.NotNull(FindDropDown(cut)));
    }

    [Fact]
    public async Task UpdateCheck_AdminViewer_HidesChannelDropdown()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.Admin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ReleaseChannelTitle"], cut.Markup));
        Assert.Null(FindDropDown(cut));
    }

    [Fact]
    public async Task UpdateCheck_AdminViewer_ShowsReadOnlyChannelAndNote()
    {
        SetupChannel(UpdateReleaseChannel.Testing);
        await SignInAsync(OperatorRole.Admin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ChannelTesting"], cut.Markup));
        Assert.Contains(Localizer["ReadOnlyChannelNote"], cut.Markup);
    }

    [Fact]
    public async Task UpdateCheck_SuperAdminSelectsDifferentChannel_SavesOnceWithoutButtonClick()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        _ = _releaseChannel.Setup(s => s.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateReleaseChannel.Testing);
        await SignInAsync(OperatorRole.SuperAdmin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();
        cut.WaitForAssertion(() => Assert.NotNull(FindDropDown(cut)));

        IRenderedComponent<SfDropDownList<UpdateReleaseChannel, KeyValuePair<UpdateReleaseChannel, string>>> dropDown = FindDropDown(cut)!;
        await cut.InvokeAsync(() => dropDown.Instance.ValueChanged.InvokeAsync(UpdateReleaseChannel.Testing));

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ChannelSavedMessage"], cut.Markup));
        _releaseChannel.Verify(
            s => s.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCheck_SuperAdminSelectsCurrentChannel_DoesNotCallServer()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.SuperAdmin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();
        cut.WaitForAssertion(() => Assert.NotNull(FindDropDown(cut)));

        IRenderedComponent<SfDropDownList<UpdateReleaseChannel, KeyValuePair<UpdateReleaseChannel, string>>> dropDown = FindDropDown(cut)!;
        await cut.InvokeAsync(() => dropDown.Instance.ValueChanged.InvokeAsync(UpdateReleaseChannel.Stable));

        _releaseChannel.Verify(
            s => s.SetReleaseChannelAsync(It.IsAny<UpdateReleaseChannel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateCheck_ChannelCard_RendersBeforeCheckNowButton()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.SuperAdmin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ReleaseChannelTitle"], cut.Markup));
        int cardIndex = cut.Markup.IndexOf(Localizer["ReleaseChannelTitle"].Value, StringComparison.Ordinal);
        int buttonIndex = cut.Markup.IndexOf(Localizer["CheckNowButton"].Value, StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0);
        Assert.True(cardIndex < buttonIndex, "Release channel card must render before the check-now button.");
    }

    [Fact]
    public async Task UpdateCheck_ChannelLoadFails_ShowsLocalizedLoadFailedMessage()
    {
        _ = _releaseChannel.Setup(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));
        await SignInAsync(OperatorRole.SuperAdmin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ChannelLoadFailed", "boom"], cut.Markup));
    }

    [Fact]
    public async Task UpdateCheck_ReadOnlyViewer_HidesChannelCardAndSendsNoChannelRequest()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.ReadOnly);

        IRenderedComponent<UpdateCheck> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["CurrentVersionLabel"], cut.Markup));
        Assert.DoesNotContain(Localizer["ReleaseChannelTitle"], cut.Markup);
        _releaseChannel.Verify(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCheck_ReviewViewer_HidesChannelCardAndSendsNoChannelRequest()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.Review);

        IRenderedComponent<UpdateCheck> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["CurrentVersionLabel"], cut.Markup));
        Assert.DoesNotContain(Localizer["ReleaseChannelTitle"], cut.Markup);
        _releaseChannel.Verify(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCheck_AdminViewer_ShowsChannelCardAndLoadsChannel()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.Admin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ReleaseChannelTitle"], cut.Markup));
        _releaseChannel.Verify(s => s.GetReleaseChannelAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCheck_JustNowTimestamp_RendersLocalizedJustNow()
    {
        _ = _updateCheck.Setup(s => s.GetState())
            .Returns(new UpdateCheckState(false, null, "1.0.0", null, DateTime.UtcNow.AddSeconds(-10), null));
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.SuperAdmin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["JustNow"], cut.Markup));
    }

    [Fact]
    public async Task UpdateCheck_MinutesAgoTimestamp_RendersLocalizedPlural()
    {
        _ = _updateCheck.Setup(s => s.GetState())
            .Returns(new UpdateCheckState(false, null, "1.0.0", null, DateTime.UtcNow.AddMinutes(-5).AddSeconds(-5), null));
        SetupChannel(UpdateReleaseChannel.Stable);
        await SignInAsync(OperatorRole.SuperAdmin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains(Localizer["MinutesAgo", 5], cut.Markup));
    }

    [Fact]
    public async Task UpdateCheck_ChannelSaveFails_ShowsSaveFailedAndRevertsSelection()
    {
        SetupChannel(UpdateReleaseChannel.Stable);
        _ = _releaseChannel.Setup(s => s.SetReleaseChannelAsync(It.IsAny<UpdateReleaseChannel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("denied"));
        await SignInAsync(OperatorRole.SuperAdmin);

        IRenderedComponent<UpdateCheck> cut = RenderPage();
        cut.WaitForAssertion(() => Assert.NotNull(FindDropDown(cut)));

        IRenderedComponent<SfDropDownList<UpdateReleaseChannel, KeyValuePair<UpdateReleaseChannel, string>>> dropDown = FindDropDown(cut)!;
        await cut.InvokeAsync(() => dropDown.Instance.ValueChanged.InvokeAsync(UpdateReleaseChannel.Testing));

        cut.WaitForAssertion(() => Assert.Contains(Localizer["ChannelSaveFailed", "denied"], cut.Markup));
        Assert.Equal(UpdateReleaseChannel.Stable, FindDropDown(cut)!.Instance.Value);
    }
}
