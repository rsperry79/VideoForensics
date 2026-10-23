namespace VideoForensics.Ui.Shared.Tests;

using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using Xunit;

/// <summary>
/// Shared setup for the /accounts/{Id}/schedule page tests: the sync-schedule repository mock, the real
/// <see cref="PairedSessionState"/> over the bUnit JS runtime, and the stub <see cref="HttpClient"/> used for
/// the config sync interval endpoint.
/// </summary>
public abstract class AccountSyncSchedulePageTestBase : BunitContext
{
    protected static readonly Guid AccountId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    protected StubHttpMessageHandler Handler { get; } = new();

    protected AccountSyncSchedulePageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        var scheduleRepository = new Mock<ISyncScheduleRepository>();
        scheduleRepository
            .Setup(r => r.GetByProviderAccountIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncSchedule?)null);

        Services.AddScoped(_ => scheduleRepository.Object);
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        Services.AddScoped(_ => Handler);
        Services.AddScoped(_ => new HttpClient(Handler));

        SetRendererInfo(new RendererInfo("Server", true));
    }

    protected IStringLocalizer<AccountSyncSchedule> Strings => Services.GetRequiredService<IStringLocalizer<AccountSyncSchedule>>();

    protected IRenderedComponent<AccountSyncSchedule> RenderPage() =>
        Render<AccountSyncSchedule>(p => p.Add(c => c.Id, AccountId));

    /// <summary>Absolute path (as the page requests it) for a sub-route of this account.</summary>
    protected static string ApiPath(string suffix) => $"/api/v1/accounts/provider-accounts/{AccountId}{suffix}";

    protected void SetJson(string suffix, object body) => Handler.Bodies[ApiPath(suffix)] = body;

    protected void FailJson(string suffix) => Handler.FailingPaths.Add(ApiPath(suffix));
}

public class AccountSyncSchedulePage_ConfigSyncInterval_Tests : AccountSyncSchedulePageTestBase
{
    private const string ConfigIntervalInputId = "#config-sync-interval-hours";

    [Fact]
    public void AccountSyncSchedule_ConfigSyncIntervalLoaded_ShowsHoursFromApi()
    {
        SetJson("/config-sync-interval", new ConfigSyncIntervalDto(24));

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Equal("24", cut.WaitForElement(ConfigIntervalInputId).GetAttribute("value")));
        Assert.Contains(Strings["ConfigSyncIntervalLabel"], cut.Find("#config-sync-interval").TextContent);
    }

    [Fact]
    public void AccountSyncSchedule_ConfigSyncIntervalUnavailable_ShowsDefaultOf24()
    {
        FailJson("/config-sync-interval");

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.Equal("24", cut.WaitForElement(ConfigIntervalInputId).GetAttribute("value")));
    }

    [Fact]
    public void AccountSyncSchedule_ConfigSyncIntervalChanged_SavesHoursWithPut()
    {
        SetJson("/config-sync-interval", new ConfigSyncIntervalDto(24));

        var cut = RenderPage();
        cut.WaitForElement(ConfigIntervalInputId).Change("48");

        cut.WaitForAssertion(() => Assert.Contains(Handler.Sent, s => s.Method == HttpMethod.Put && s.Path == ApiPath("/config-sync-interval")));
        var body = Handler.Sent.Single(s => s.Method == HttpMethod.Put).Body!;
        Assert.Equal(48, JsonDocument.Parse(body).RootElement.GetProperty("hours").GetInt32());
    }

    [Fact]
    public void AccountSyncSchedule_ConfigSyncIntervalOutOfRange_ShowsErrorAndDoesNotPut()
    {
        SetJson("/config-sync-interval", new ConfigSyncIntervalDto(24));

        var cut = RenderPage();
        cut.WaitForElement(ConfigIntervalInputId).Change("0");

        cut.WaitForAssertion(() => Assert.Contains(Strings["ConfigSyncIntervalInvalid"], cut.Find("#config-sync-interval").TextContent));
        Assert.DoesNotContain(Handler.Sent, s => s.Method == HttpMethod.Put);
    }
}
