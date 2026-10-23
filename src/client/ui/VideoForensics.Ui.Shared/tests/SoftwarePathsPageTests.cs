using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using Moq;

using System.Net;
using System.Text;
using System.Text.Json;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class SoftwarePathsPageTests : BunitContext
{
    private readonly SoftwarePathsHandler _handler = new();

    public SoftwarePathsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddScoped<WebAuthnClient>(sp => new StepUpStubWebAuthnClient(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddScoped<StepUpService>();
        _ = Services.AddScoped(_ => new HttpClient(_handler));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void SoftwarePathsPage_SaveWithoutStepUpToken_DoesNotPost()
    {
        // No signed-in session, so the step-up challenge cannot produce a token. This is the same failure
        // the page sees when the operator cancels the challenge.
        _handler.Bodies["/api/v1/software-paths"] = new { ffmpegBinDirectory = string.Empty };

        IRenderedComponent<SoftwarePaths> cut = Render<SoftwarePaths>();
        cut.WaitForAssertion(() => Assert.Single(_handler.Sent, s => s.Method == HttpMethod.Get));

        cut.Find("button.save-software-paths").Click();

        Assert.DoesNotContain(_handler.Sent, s => s.Method == HttpMethod.Post);
        Assert.Contains(Services.GetRequiredService<IStringLocalizer<SoftwarePaths>>()["StepUpCancelled"], cut.Markup);
    }

    [Fact]
    public async Task SoftwarePathsPage_SaveWithStepUpAndValidPaths_ShowsSavedMessage()
    {
        PairedSessionState session = await SignInWithPasskeyAsync();

        _handler.Bodies["/api/v1/software-paths"] = new { ffmpegBinDirectory = @"C:\old" };
        IRenderedComponent<SoftwarePaths> cut = Render<SoftwarePaths>();
        _ = cut.WaitForElement("#ffmpeg-bin-directory");

        // The handler answers the POST with whatever the body currently holds, so the saved values are the new ones.
        _handler.Bodies["/api/v1/software-paths"] = new { ffmpegBinDirectory = @"C:\new" };
        cut.Find("#ffmpeg-bin-directory").Change(@"C:\new");

        cut.Find("button.save-software-paths").Click();

        IStringLocalizer<SoftwarePaths> localizer = Services.GetRequiredService<IStringLocalizer<SoftwarePaths>>();
        cut.WaitForAssertion(() => Assert.Contains(localizer["SavedMessage"], cut.Markup));
        Assert.Contains(_handler.Sent, s => s.Method == HttpMethod.Post && s.Body is not null && s.Body.Contains(@"C:\\new"));
        Assert.NotNull(session.SessionToken);
    }

    [Fact]
    public async Task SoftwarePathsPage_SaveRejected_ShowsServerError()
    {
        const string serverError = @"Path does not exist: C:\missing";
        _ = await SignInWithPasskeyAsync();

        _handler.Bodies["/api/v1/software-paths"] = new { ffmpegBinDirectory = string.Empty };
        _handler.PostError = serverError;
        IRenderedComponent<SoftwarePaths> cut = Render<SoftwarePaths>();
        _ = cut.WaitForElement("#ffmpeg-bin-directory");
        cut.Find("#ffmpeg-bin-directory").Change(@"C:\missing");

        cut.Find("button.save-software-paths").Click();

        cut.WaitForAssertion(() => Assert.Contains(serverError, cut.Markup));
        Assert.DoesNotContain(Services.GetRequiredService<IStringLocalizer<SoftwarePaths>>()["SavedMessage"], cut.Markup);
    }

    /// <summary>Signs the test circuit in with a passkey session, which the step-up stub then confirms.</summary>
    private async Task<PairedSessionState> SignInWithPasskeyAsync()
    {
        PairedSessionState session = Services.GetRequiredService<PairedSessionState>();
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

        public override Task<string> StepUpAsync(string sessionToken)
        {
            return Task.FromResult("step-up-token");
        }
    }

    /// <summary>
    /// Serves the GET from <see cref="Bodies"/>. When <see cref="PostError"/> is set, the POST answers 400 with that
    /// error; otherwise the POST echoes <see cref="Bodies"/> back as a 200.
    /// </summary>
    private sealed class SoftwarePathsHandler : HttpMessageHandler
    {
        public Dictionary<string, object> Bodies { get; } = [];
        public string? PostError { get; set; }
        public List<(HttpMethod Method, string Path, string? Body)> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            string? sentBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Sent.Add((request.Method, path, sentBody));

            return request.Method == HttpMethod.Post && PostError is not null
                ? Json(HttpStatusCode.BadRequest, new { error = PostError })
                : !Bodies.TryGetValue(path, out object? body) ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(HttpStatusCode.OK, body);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, object body)
        {
            return new(status)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
        }
    }
}
