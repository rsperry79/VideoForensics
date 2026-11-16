
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using AngleSharp.Dom;

using System.Net;
using System.Text;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// The pending-credential section on /settings/operators must offer approve and revoke actions for each
/// pending credential.
/// </summary>
public class SecurityOperatorsPendingTests : BunitContext
{
    private static readonly Guid PendingCredentialId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid OperatorId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

    private sealed class PendingCredentialHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            string json = path.EndsWith("/operator-credentials/pending", StringComparison.Ordinal)
                ? $"[{{\"id\":\"{PendingCredentialId}\",\"operatorId\":\"{OperatorId}\",\"label\":\"Work phone\",\"webAuthnCredentialId\":\"AAAA\",\"webAuthnPublicKey\":\"AQID\",\"isApproved\":false,\"createdAtUtc\":\"2026-01-01T00:00:00Z\"}}]"
                : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeSelfApiHttpClientFactory : ISelfApiHttpClientFactory
    {
        public HttpClient CreateClient()
        {
            return CreateClient(null);
        }

        public HttpClient CreateClient(string? bearerToken)
        {
            return new(new PendingCredentialHandler()) { BaseAddress = new Uri("http://videoforensics.example.com/") };
        }
    }

    [Fact]
    public async Task SecurityOperators_PendingCredential_ShowsApproveAndRevokeActions()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddLocalization();

        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), OperatorRole.SuperAdmin.ToString());
        _ = Services.AddScoped(_ => session);
        _ = Services.AddScoped<ISelfApiHttpClientFactory>(_ => new FakeSelfApiHttpClientFactory());
        _ = Services.AddScoped(_ => new Moq.Mock<WebAuthnClient>(Moq.Mock.Of<IJSRuntime>(), Moq.Mock.Of<ISelfApiHttpClientFactory>()).Object);
        _ = Services.AddScoped<StepUpService>();

        IRenderedComponent<SecurityOperators> cut = Render<SecurityOperators>();
        IStringLocalizer<SecurityOperators> localizer = Services.GetRequiredService<IStringLocalizer<SecurityOperators>>();

        cut.WaitForAssertion(() => Assert.Contains("Work phone", cut.Markup));

        IReadOnlyList<IElement> buttons = cut.FindAll("button");
        Assert.Contains(buttons, b => b.TextContent.Trim() == localizer["ApproveButton"].Value);
        Assert.Contains(buttons, b => b.TextContent.Trim() == localizer["RevokeButton"].Value);
    }
}
