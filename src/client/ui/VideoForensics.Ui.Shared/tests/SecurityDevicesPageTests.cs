
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using System.Net;
using System.Text;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>
/// Device-level pairing UI (generate QR / pairing URL) is removed from SecurityDevices.razor.
/// The paired-devices list and revoke UI must remain. See Pair.razor for the retained code.
/// </summary>
public class SecurityDevicesPageTests : BunitContext
{
    private sealed class EmptyDevicesHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
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
            return new(new EmptyDevicesHandler()) { BaseAddress = new Uri("http://videoforensics.example.com/") };
        }
    }

    [Fact]
    public async Task SecurityDevices_PairingUi_NotRendered()
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

        IRenderedComponent<SecurityDevices> cut = Render<SecurityDevices>();

        string generateLabel = Services.GetRequiredService<IStringLocalizer<SecurityDevices>>()["GenerateQrButton"].Value;
        Assert.DoesNotContain(generateLabel, cut.Markup);
        Assert.Empty(cut.FindAll("img[alt='Pairing QR code']"));
    }
}
