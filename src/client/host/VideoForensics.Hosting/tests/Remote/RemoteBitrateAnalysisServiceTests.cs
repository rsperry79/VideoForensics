using System.Net;
using System.Text.Json;

using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Tests.Remote
{
    public class RemoteBitrateAnalysisServiceTests
    {
        private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = From.AddDays(7);

        private sealed class CapturingHandler(string json) : HttpMessageHandler
        {
            public HttpRequestMessage? Request { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                });
            }
        }

        private static SignalStatsDto Stats(int count)
        {
            return new(count, null, null, 0, null, 0, null);
        }

        [Fact]
        public async Task AnalyzeAsync_NoDevices_ReturnsEmptyWithoutCallingServer()
        {
            var handler = new CapturingHandler("{}");
            var service = new RemoteBitrateAnalysisService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            BitrateAnalysis result = await service.AnalyzeAsync([], From, To, CancellationToken.None);

            Assert.Empty(result.Devices);
            Assert.Null(handler.Request);
        }

        [Fact]
        public async Task AnalyzeAsync_WithDevices_CallsRouteWithEscapedQueryAndMapsResponse()
        {
            var id = Guid.NewGuid();
            var cells = Enumerable.Range(0, 168).Select(_ => Stats(0)).ToList();
            var hours = Enumerable.Range(0, 24).Select(_ => Stats(0)).ToList();
            var days = Enumerable.Range(0, 7).Select(_ => Stats(0)).ToList();
            var dto = new BitrateAnalysisDto([
                new BitrateDeviceAnalysisDto(
                    id, "Front door", "Ring", "1234", Stats(1), Stats(1),
                    [new BitrateSampleDto(From.AddHours(1), 2_000, "Day", true)],
                    [], cells, hours, days, [], [], [])]);
            var handler = new CapturingHandler(JsonSerializer.Serialize(dto));
            var service = new RemoteBitrateAnalysisService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            BitrateAnalysis result = await service.AnalyzeAsync([id], From, To, CancellationToken.None);

            Assert.NotNull(handler.Request);
            Assert.Equal(HttpMethod.Get, handler.Request.Method);
            string url = handler.Request.RequestUri!.AbsoluteUri;
            Assert.StartsWith("https://example.test/api/v1/bitrate-analysis?devices=", url);
            Assert.Contains(Uri.EscapeDataString(id.ToString("D")), url);
            Assert.Contains("&from=" + Uri.EscapeDataString(From.ToString("O")), url);
            Assert.Contains("&to=" + Uri.EscapeDataString(To.ToString("O")), url);
            BitrateDeviceAnalysis device = Assert.Single(result.Devices);
            Assert.Equal(new BitrateSample(From.AddHours(1), 2_000, "Day", true), Assert.Single(device.Readings));
        }
    }
}
