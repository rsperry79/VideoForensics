namespace VideoForensics.Ui.Shared.Tests;

using System.Net;
using System.Text;

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

using Xunit;

/// <summary>
/// Device-level pairing UI (generate QR / pairing URL) is removed from SecurityDevices.razor.
/// The paired-devices list and revoke UI must remain. See Pair.razor for the retained code.
/// </summary>
public class SecurityDevicesPageTests : BunitContext
{
    private sealed class EmptyDevicesHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
    }

    private sealed class FakeSelfApiHttpClientFactory : ISelfApiHttpClientFactory
    {
        public HttpClient CreateClient() => CreateClient(null);

        public HttpClient CreateClient(string? bearerToken) =>
            new(new EmptyDevicesHandler()) { BaseAddress = new Uri("http://videoforensics.example.com/") };
    }

    [Fact]
    public async Task SecurityDevices_PairingUi_NotRendered()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddLocalization();

        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), OperatorRole.SuperAdmin.ToString());
        Services.AddScoped(_ => session);
        Services.AddScoped<ISelfApiHttpClientFactory>(_ => new FakeSelfApiHttpClientFactory());
        Services.AddScoped(_ => new Moq.Mock<WebAuthnClient>(Moq.Mock.Of<IJSRuntime>(), Moq.Mock.Of<ISelfApiHttpClientFactory>()).Object);
        Services.AddScoped<StepUpService>();

        var cut = Render<SecurityDevices>();

        var generateLabel = Services.GetRequiredService<IStringLocalizer<SecurityDevices>>()["GenerateQrButton"].Value;
        Assert.DoesNotContain(generateLabel, cut.Markup);
        Assert.Empty(cut.FindAll("img[alt='Pairing QR code']"));
    }
}
