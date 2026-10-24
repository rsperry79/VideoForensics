using System.Net;
using System.Text.Json;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Client.Ui.Shared.Services;

namespace VideoForensics.Client.Hosting.Tests.Remote;

public class RemoteDeviceControlServiceTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handleRequest;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handleRequest)
        {
            _handleRequest = handleRequest;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handleRequest(request);
        }
    }

    private static (IDeviceControlService Service, List<HttpRequestMessage> Requests, List<string> Bodies) Build(HttpStatusCode status = HttpStatusCode.NoContent)
    {
        var requests = new List<HttpRequestMessage>();
        var bodies = new List<string>();

        var handler = new MockHttpMessageHandler(async request =>
        {
            requests.Add(request);
            bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync());
            return new HttpResponseMessage(status);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        return (new RemoteDeviceControlService(httpClient), requests, bodies);
    }

    [Fact]
    public async Task SetLightAsync_OnTrue_PostsLightRouteWithOnBody()
    {
        (IDeviceControlService? service, List<HttpRequestMessage>? requests, List<string>? bodies) = Build();

        await service.SetLightAsync("123", true, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.Equal("/api/v1/device-controls/123/light", requests[0].RequestUri?.PathAndQuery);
        Assert.True(JsonDocument.Parse(bodies[0]).RootElement.GetProperty("on").GetBoolean());
    }

    [Fact]
    public async Task SetSirenAsync_WithDuration_PostsSirenRouteWithOnAndDurationBody()
    {
        (IDeviceControlService? service, List<HttpRequestMessage>? requests, List<string>? bodies) = Build();

        await service.SetSirenAsync("123", true, 30, CancellationToken.None);

        Assert.Equal("/api/v1/device-controls/123/siren", requests[0].RequestUri?.PathAndQuery);
        JsonElement root = JsonDocument.Parse(bodies[0]).RootElement;
        Assert.True(root.GetProperty("on").GetBoolean());
        Assert.Equal(30, root.GetProperty("durationSeconds").GetInt32());
    }

    [Fact]
    public async Task SetVolumeAsync_Volume_PostsVolumeRouteWithVolumeBody()
    {
        (IDeviceControlService? service, List<HttpRequestMessage>? requests, List<string>? bodies) = Build();

        await service.SetVolumeAsync("123", 7, CancellationToken.None);

        Assert.Equal("/api/v1/device-controls/123/volume", requests[0].RequestUri?.PathAndQuery);
        Assert.Equal(7, JsonDocument.Parse(bodies[0]).RootElement.GetProperty("volume").GetInt32());
    }

    [Fact]
    public async Task SetNightModeAsync_Enabled_PostsNightModeRouteWithEnabledBody()
    {
        (IDeviceControlService? service, List<HttpRequestMessage>? requests, List<string>? bodies) = Build();

        await service.SetNightModeAsync("123", false, CancellationToken.None);

        Assert.Equal("/api/v1/device-controls/123/night-mode", requests[0].RequestUri?.PathAndQuery);
        Assert.False(JsonDocument.Parse(bodies[0]).RootElement.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task SetMotionDetectionAsync_Enabled_PostsMotionDetectionRouteWithEnabledBody()
    {
        (IDeviceControlService? service, List<HttpRequestMessage>? requests, List<string>? bodies) = Build();

        await service.SetMotionDetectionAsync("123", true, CancellationToken.None);

        Assert.Equal("/api/v1/device-controls/123/motion-detection", requests[0].RequestUri?.PathAndQuery);
        Assert.True(JsonDocument.Parse(bodies[0]).RootElement.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task SetDoNotDisturbAsync_Seconds_PostsDoNotDisturbRouteWithSecondsBody()
    {
        (IDeviceControlService? service, List<HttpRequestMessage>? requests, List<string>? bodies) = Build();

        await service.SetDoNotDisturbAsync("123", 600, CancellationToken.None);

        Assert.Equal("/api/v1/device-controls/123/do-not-disturb", requests[0].RequestUri?.PathAndQuery);
        Assert.Equal(600, JsonDocument.Parse(bodies[0]).RootElement.GetProperty("seconds").GetInt32());
    }

    [Fact]
    public async Task SetLightAsync_NonSuccessResponse_ThrowsWithoutUrl()
    {
        (IDeviceControlService? service, List<HttpRequestMessage> _, List<string> _) = Build(HttpStatusCode.BadGateway);

        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.SetLightAsync("123", true, CancellationToken.None));

        Assert.DoesNotContain("example.test", ex.Message);
        Assert.DoesNotContain("http", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetLightAsync_CancelledToken_ThrowsOperationCanceled()
    {
        (IDeviceControlService? service, List<HttpRequestMessage> _, List<string> _) = Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SetLightAsync("123", true, cts.Token));
    }

    [Fact]
    public async Task SetLightAsync_SignedInSession_SendsBearerToken()
    {
        var js = new Mock<Microsoft.JSInterop.IJSRuntime>();
        var state = new PairedSessionState(js.Object);
        await state.SetAsync("token-abc", Guid.NewGuid(), "SuperAdmin");

        HttpRequestMessage? captured = null;
        var inner = new MockHttpMessageHandler(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var authHandler = new PairedDeviceAuthHandler(state) { InnerHandler = inner };
        var httpClient = new HttpClient(authHandler) { BaseAddress = new Uri("https://example.test") };
        var service = new RemoteDeviceControlService(httpClient);

        await service.SetLightAsync("123", true, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("token-abc", captured.Headers.Authorization?.Parameter);
    }
}
