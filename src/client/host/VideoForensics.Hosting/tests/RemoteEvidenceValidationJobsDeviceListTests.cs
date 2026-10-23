using System.Net;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    /// <summary>The device-list integrity start sends every device in one request and reads back per-device results.</summary>
    public class RemoteEvidenceValidationJobsDeviceListTests
    {
        [Fact]
        public async Task StartIntegrityForDevicesAsync_SendsEveryDeviceInOneRequest()
        {
            Guid first = Guid.NewGuid();
            Guid second = Guid.NewGuid();
            string? capturedBody = null;
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedBody = await request.Content!.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(BuildDto(Guid.NewGuid())), System.Text.Encoding.UTF8, "application/json")
                };
            });
            var jobs = new RemoteEvidenceValidationJobs(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            _ = await jobs.StartIntegrityForDevicesAsync(new[] { first, second }, CancellationToken.None);

            StartIntegrityJobRequest? sent = JsonSerializer.Deserialize<StartIntegrityJobRequest>(capturedBody!);
            Assert.NotNull(sent);
            Assert.Null(sent!.DeviceId);
            Assert.Equal(new[] { first, second }, sent.DeviceIds);
        }

        [Fact]
        public async Task StartIntegrityForDevicesAsync_ReadsBackDeviceIdOnResults()
        {
            Guid device = Guid.NewGuid();
            var dto = BuildDto(Guid.NewGuid()) with
            {
                IntegrityResults = new List<MediaVerificationResultDto>
                {
                    new(Guid.NewGuid(), "a.mp4", "verified", null, device),
                }
            };
            var handler = new CaptureHttpMessageHandler((request, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json")
            }));
            var jobs = new RemoteEvidenceValidationJobs(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });

            EvidenceValidationJob job = await jobs.StartIntegrityForDevicesAsync(new[] { device }, CancellationToken.None);

            Assert.NotNull(job.IntegrityResults);
            Assert.Equal(device, job.IntegrityResults![0].DeviceId);
        }

        private sealed class CaptureHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public CaptureHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                _handler(request, cancellationToken);
        }

        private static EvidenceValidationJobDto BuildDto(Guid id) => new(
            Id: id,
            Kind: "Integrity",
            State: "Running",
            StartedUtc: new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            CompletedUtc: null,
            Log: Array.Empty<string>(),
            IntegrityResults: null,
            Discrepancies: null,
            ErrorMessage: null);
    }
}
