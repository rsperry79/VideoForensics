namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

/// <summary>
/// Tests for the "Display mode" section of OperatorDetails.razor. The page's own operator fetch uses a
/// raw HttpClient that cannot succeed under bUnit, so these also prove the section does not depend on
/// that fetch succeeding.
/// </summary>
public class OperatorDetailsDisplayModeTests : BunitContext
{
    private static readonly Guid TargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ViewerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly Mock<IAdminOperatorService> _admin = new();
    private readonly PairedSessionState _session;

    public OperatorDetailsDisplayModeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddLocalization();

        Services.AddScoped(_ => _admin.Object);
        Services.AddScoped(_ => new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>()).Object);
        _session = new PairedSessionState(JSInterop.JSRuntime);
        Services.AddScoped(_ => _session);

        _admin.Setup(s => s.GetUiModeAsync(TargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperatorUiMode("Standard", false));
        _admin.Setup(s => s.SetUiModeAsync(TargetId, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    private async Task SignInAsync(OperatorRole role, Guid? operatorId = null)
    {
        await _session.SetAsync("test-token", operatorId ?? ViewerId, role.ToString());
    }

    private IRenderedComponent<OperatorDetails> RenderPage()
        => Render<OperatorDetails>(p => p.Add(c => c.Id, TargetId));

    private static string? SelectedValue(IRenderedComponent<OperatorDetails> cut)
        => cut.FindAll("select.display-mode-select option").FirstOrDefault(o => o.HasAttribute("selected"))?.GetAttribute("value");

    [Theory]
    [InlineData(OperatorRole.Admin)]
    [InlineData(OperatorRole.SuperAdmin)]
    public async Task Page_AdminOrSuperAdminViewer_RendersDisplayModeSection(OperatorRole role)
    {
        await SignInAsync(role);

        var cut = RenderPage();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("select.display-mode-select"));
            Assert.NotEmpty(cut.FindAll("input.display-mode-lock"));
            Assert.NotEmpty(cut.FindAll("button.display-mode-save"));
        });
    }

    [Theory]
    [InlineData(OperatorRole.ReadOnly)]
    [InlineData(OperatorRole.Review)]
    public async Task Page_ReviewOrReadOnlyViewer_DoesNotRenderDisplayModeSection(OperatorRole role)
    {
        await SignInAsync(role);

        var cut = RenderPage();

        Assert.Empty(cut.FindAll("select.display-mode-select"));
        Assert.Empty(cut.FindAll("button.display-mode-save"));
        _admin.Verify(s => s.GetUiModeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Page_SignedOutViewer_DoesNotRenderDisplayModeSection()
    {
        var cut = RenderPage();

        Assert.Empty(cut.FindAll("select.display-mode-select"));
        Assert.Empty(cut.FindAll("button.display-mode-save"));
        _admin.Verify(s => s.GetUiModeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Page_StoredSimpleAndLocked_ShowsStoredValues()
    {
        await SignInAsync(OperatorRole.SuperAdmin);
        _admin.Setup(s => s.GetUiModeAsync(TargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperatorUiMode("Simple", true));

        var cut = RenderPage();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Simple", SelectedValue(cut));
            Assert.True(cut.Find("input.display-mode-lock").HasAttribute("checked"));
        });
    }

    [Fact]
    public async Task Save_ChangedModeAndLock_CallsSetUiModeOnceAndShowsSuccess()
    {
        await SignInAsync(OperatorRole.Admin);
        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("select.display-mode-select")));

        cut.Find("select.display-mode-select").Change("Simple");
        cut.Find("input.display-mode-lock").Change(true);
        cut.Find("button.display-mode-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("Saved. Takes effect the next time the user loads a page.", cut.Markup));
        _admin.Verify(s => s.SetUiModeAsync(TargetId, "Simple", true, It.IsAny<CancellationToken>()), Times.Once);
        _admin.Verify(s => s.SetUiModeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Save_ViewingOwnOperator_StillAllowed()
    {
        await SignInAsync(OperatorRole.SuperAdmin, operatorId: TargetId);
        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.display-mode-save")));

        cut.Find("button.display-mode-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("Saved.", cut.Markup));
        _admin.Verify(s => s.SetUiModeAsync(TargetId, "Standard", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Save_ServiceThrows_ShowsErrorAndNoSuccessMessage()
    {
        await SignInAsync(OperatorRole.Admin);
        _admin.Setup(s => s.SetUiModeAsync(TargetId, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom-from-server"));
        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.display-mode-save")));

        cut.Find("button.display-mode-save").Click();

        cut.WaitForAssertion(() => Assert.Contains("boom-from-server", cut.Markup));
        Assert.DoesNotContain("Takes effect the next time", cut.Markup);
        Assert.False(cut.Find("button.display-mode-save").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Load_ServiceThrows_ShowsLoadFailedAndRestOfPageStillRenders()
    {
        await SignInAsync(OperatorRole.Admin);
        _admin.Setup(s => s.GetUiModeAsync(TargetId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("load-boom"));

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Contains("Could not load the display mode", cut.Markup));
        Assert.Contains("Operator Details", cut.Markup);
    }

    [Fact]
    public async Task Save_CallPending_DisablesSaveButtonUntilItCompletes()
    {
        await SignInAsync(OperatorRole.Admin);
        var gate = new TaskCompletionSource();
        _admin.Setup(s => s.SetUiModeAsync(TargetId, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(gate.Task);
        var cut = RenderPage();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.display-mode-save")));

        cut.Find("button.display-mode-save").Click();

        cut.WaitForAssertion(() => Assert.True(cut.Find("button.display-mode-save").HasAttribute("disabled")));
        gate.SetResult();
        cut.WaitForAssertion(() => Assert.False(cut.Find("button.display-mode-save").HasAttribute("disabled")));
    }
}