using System.Net;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests;

public class RemoteDeviceTypedSettingsReaderTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        public Uri? RequestedUri { get; private set; }

        public StubHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task RemoteDeviceTypedSettingsReader_GetAsync_CallsTypedRouteAndMapsRows()
    {
        Guid deviceId = Guid.NewGuid();
        var dto = new DeviceTypedSettingsDto(
            deviceId,
            new DeviceOfflineMotionDto(true, false, 5000, 360, 2, 30),
            [new DeviceDetectionTypeDto("human", true, "cloud", false, true)],
            [new DeviceDetectionThresholdDto("loitering", 10.0)]);
        var handler = new StubHandler(JsonSerializer.Serialize(dto));
        var reader = new RemoteDeviceTypedSettingsReader(new HttpClient(handler) { BaseAddress = new Uri("https://server.test/") });

        var settings = await reader.GetAsync(deviceId, CancellationToken.None);

        Assert.Equal($"https://server.test/api/v1/device-config/typed/{deviceId}", handler.RequestedUri!.ToString());
        Assert.Equal(5000, settings.OfflineMotion!.MaxUploadKb);
        Assert.Equal(deviceId, settings.OfflineMotion.DeviceId);
        Assert.Equal("cloud", Assert.Single(settings.DetectionTypes).Mode);
        Assert.Equal(10.0, Assert.Single(settings.Thresholds).Threshold);
    }
}
