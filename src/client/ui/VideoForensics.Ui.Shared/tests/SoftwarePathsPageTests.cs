namespace VideoForensics.Ui.Shared.Tests;

using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using Xunit;

public class SoftwarePathsPageTests : BunitContext
{
    private readonly StubHttpMessageHandler _handler = new();

    public SoftwarePathsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        Services.AddScoped(sp => new WebAuthnClient(sp.GetRequiredService<IJSRuntime>(), new Mock<ISelfApiHttpClientFactory>().Object));
        Services.AddScoped<StepUpService>();
        Services.AddScoped(_ => new HttpClient(_handler));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void SoftwarePathsPage_SaveWithoutStepUpToken_DoesNotPost()
    {
        // No signed-in session, so the step-up challenge cannot produce a token. This is the same failure
        // the page sees when the operator cancels the challenge.
        _handler.Bodies["/api/software-paths"] = new { ffmpegBinDirectory = "", cloudflaredPath = "" };

        var cut = Render<SoftwarePaths>();
        cut.WaitForAssertion(() => Assert.Single(_handler.Sent, s => s.Method == HttpMethod.Get));

        cut.Find("button.save-software-paths").Click();

        Assert.DoesNotContain(_handler.Sent, s => s.Method == HttpMethod.Post);
        Assert.Contains(Services.GetRequiredService<IStringLocalizer<SoftwarePaths>>()["StepUpCancelled"], cut.Markup);
    }
}
