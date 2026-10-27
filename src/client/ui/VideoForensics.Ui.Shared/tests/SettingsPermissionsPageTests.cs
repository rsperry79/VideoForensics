
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using System.Net;
using System.Net.Http.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// SettingsPermissions.razor reads and writes the provider-account edit setting through the injected
/// <see cref="ISelfApiHttpClientFactory"/>, the same way SecurityLockoutPolicy does, so the request carries the
/// session's bearer token and the server sees it as a real operator call.
/// </summary>
public class SettingsPermissionsPageTests : BunitContext
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        /// <summary>Body returned by GET /api/v1/permissions/provider-accounts; tests override it to pick the stored value.</summary>
        public object SettingBody { get; set; } = new { providerAccountEditAccess = "Admin" };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(SettingBody)
            });
        }
    }

    private sealed class FakeSelfApiHttpClientFactory(RecordingHandler handler, PairedSessionState sessionState) : ISelfApiHttpClientFactory
    {
        public HttpClient CreateClient()
        {
            return CreateClient(sessionState.SessionToken);
        }

        public HttpClient CreateClient(string? bearerToken)
        {
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://videoforensics.example.com/") };
            if (bearerToken is not null)
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            }

            return client;
        }
    }

    private readonly RecordingHandler _handler = new();

    public SettingsPermissionsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddLocalization();
    }

    private async Task RegisterSignedInSessionAsync()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), OperatorRole.SuperAdmin.ToString());
        _ = Services.AddScoped(_ => session);
        _ = Services.AddScoped<ISelfApiHttpClientFactory>(_ => new FakeSelfApiHttpClientFactory(_handler, session));
    }

    [Fact]
    public async Task SettingsPermissions_PageOpen_ReadsSettingThroughInjectedFactoryClient()
    {
        await RegisterSignedInSessionAsync();

        _ = Render<SettingsPermissions>();

        Assert.Contains(
            _handler.Requests,
            r => r.RequestUri!.AbsolutePath.EndsWith("/api/v1/permissions/provider-accounts", StringComparison.Ordinal));
        Assert.All(_handler.Requests, r => Assert.Equal("test-token", r.Headers.Authorization!.Parameter));
    }

    [Fact]
    public async Task SettingsPermissions_RendersBothEditChoices_WithLocalizedLabels()
    {
        await RegisterSignedInSessionAsync();
        IStringLocalizer<SettingsPermissions> localizer = Services.GetRequiredService<IStringLocalizer<SettingsPermissions>>();

        IRenderedComponent<SettingsPermissions> cut = Render<SettingsPermissions>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(localizer["ProviderEditAdminOption"].Value, cut.Markup, StringComparison.Ordinal);
            Assert.Contains(localizer["ProviderEditSuperAdminOption"].Value, cut.Markup, StringComparison.Ordinal);
        });
    }
}
