namespace VideoForensics.Ui.Shared.Tests;

using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
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
    private readonly SoftwarePathsHandler _handler = new();

    public SoftwarePathsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        Services.AddScoped<WebAuthnClient>(sp => new StepUpStubWebAuthnClient(sp.GetRequiredService<IJSRuntime>()));
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

    [Fact]
    public async Task SoftwarePathsPage_SaveWithStepUpAndValidPaths_ShowsSavedMessage()
    {
        var session = await SignInWithPasskeyAsync();

        _handler.Bodies["/api/software-paths"] = new { ffmpegBinDirectory = @"C:\old", cloudflaredPath = "" };
        var cut = Render<SoftwarePaths>();
        cut.WaitForElement("#ffmpeg-bin-directory");

        // The handler answers the POST with whatever the body currently holds, so the saved values are the new ones.
        _handler.Bodies["/api/software-paths"] = new { ffmpegBinDirectory = @"C:\new", cloudflaredPath = @"C:\cf\cloudflared.exe" };
        cut.Find("#ffmpeg-bin-directory").Change(@"C:\new");
        cut.Find("#cloudflared-path").Change(@"C:\cf\cloudflared.exe");

        cut.Find("button.save-software-paths").Click();

        var localizer = Services.GetRequiredService<IStringLocalizer<SoftwarePaths>>();
        cut.WaitForAssertion(() => Assert.Contains(localizer["SavedMessage"], cut.Markup));
        Assert.Contains(_handler.Sent, s => s.Method == HttpMethod.Post && s.Body is not null && s.Body.Contains(@"C:\\new"));
        Assert.NotNull(session.SessionToken);
    }

    [Fact]
    public async Task SoftwarePathsPage_SaveRejected_ShowsServerError()
    {
        const string serverError = @"Path does not exist: C:\missing";
        await SignInWithPasskeyAsync();

        _handler.Bodies["/api/software-paths"] = new { ffmpegBinDirectory = "", cloudflaredPath = "" };
        _handler.PostError = serverError;
        var cut = Render<SoftwarePaths>();
        cut.WaitForElement("#ffmpeg-bin-directory");
        cut.Find("#ffmpeg-bin-directory").Change(@"C:\missing");

        cut.Find("button.save-software-paths").Click();

        cut.WaitForAssertion(() => Assert.Contains(serverError, cut.Markup));
        Assert.DoesNotContain(Services.GetRequiredService<IStringLocalizer<SoftwarePaths>>()["SavedMessage"], cut.Markup);
    }

    /// <summary>Signs the test circuit in with a passkey session, which the step-up stub then confirms.</summary>
    private async Task<PairedSessionState> SignInWithPasskeyAsync()
    {
        var session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("session-token", Guid.NewGuid(), "SuperAdmin");
        session.SetServerSessionKind(localTier: false, passkeySession: true);
        return session;
    }

    /// <summary>Skips the real passkey ceremony and hands back a fixed step-up token.</summary>
    private sealed class StepUpStubWebAuthnClient : WebAuthnClient
    {
        public StepUpStubWebAuthnClient(IJSRuntime js)
            : base(js, new Mock<ISelfApiHttpClientFactory>().Object)
        {
        }

        public override Task<string> StepUpAsync(string sessionToken) => Task.FromResult("step-up-token");
    }

    /// <summary>
    /// Serves the GET from <see cref="Bodies"/>. When <see cref="PostError"/> is set, the POST answers 400 with that
    /// error; otherwise the POST echoes <see cref="Bodies"/> back as a 200.
    /// </summary>
    private sealed class SoftwarePathsHandler : HttpMessageHandler
    {
        public Dictionary<string, object> Bodies { get; } = new();
        public string? PostError { get; set; }
        public List<(HttpMethod Method, string Path, string? Body)> Sent { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            string? sentBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Sent.Add((request.Method, path, sentBody));

            if (request.Method == HttpMethod.Post && PostError is not null)
            {
                return Json(HttpStatusCode.BadRequest, new { error = PostError });
            }

            if (!Bodies.TryGetValue(path, out var body))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return Json(HttpStatusCode.OK, body);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
            new(status)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
    }
}
