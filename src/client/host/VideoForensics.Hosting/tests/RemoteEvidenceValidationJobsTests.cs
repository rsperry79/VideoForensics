using System.Net;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteEvidenceValidationJobsTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static EvidenceValidationJobDto BuildDto(Guid id, string kind, string state)
        {
            return new(
            Id: id,
            Kind: kind,
            State: state,
            StartedUtc: new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            CompletedUtc: new DateTime(2026, 1, 2, 3, 9, 5, DateTimeKind.Utc),
            Log: new[] { "03:04:05 started", "03:09:05 failed" },
            IntegrityResults: null,
            Discrepancies:
            [
                new(DiscrepancyType.MissingFromProvider, "event-456", null, null, null)
            ],
            ErrorMessage: "provider timeout");
        }

        [Fact]
        public async Task StartIntegrityAsync_CallsIntegrityJobRoute()
        {
            string? capturedUri = null;
            string? capturedMethod = null;
            var jobId = Guid.NewGuid();

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                capturedMethod = request.Method.Method;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(BuildDto(jobId, "Integrity", "Running")), System.Text.Encoding.UTF8, "application/json")
                };
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var jobs = new RemoteEvidenceValidationJobs(httpClient);

            EvidenceValidationJob job = await jobs.StartIntegrityAsync(null, CancellationToken.None);

            Assert.EndsWith("/api/v1/evidence/jobs/integrity", capturedUri);
            Assert.Equal("POST", capturedMethod);
            Assert.Equal(jobId, job.Id);
        }

        [Fact]
        public async Task StartReconcileAsync_CallsReconcileJobRoute()
        {
            string? capturedUri = null;
            var jobId = Guid.NewGuid();

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(BuildDto(jobId, "Reconcile", "Running")), System.Text.Encoding.UTF8, "application/json")
                };
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var jobs = new RemoteEvidenceValidationJobs(httpClient);

            _ = await jobs.StartReconcileAsync(Guid.NewGuid(), "ring-device-123", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.EndsWith("/api/v1/evidence/jobs/reconcile", capturedUri);
        }

        [Fact]
        public async Task GetLatestAsync_MapsDtoToDomainObject()
        {
            var jobId = Guid.NewGuid();
            string? capturedUri = null;

            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
            {
                capturedUri = request.RequestUri?.ToString();
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(BuildDto(jobId, "Reconcile", "Failed")), System.Text.Encoding.UTF8, "application/json")
                };
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var jobs = new RemoteEvidenceValidationJobs(httpClient);

            EvidenceValidationJob? job = await jobs.GetLatestAsync(EvidenceValidationJobKind.Reconcile, CancellationToken.None);

            Assert.EndsWith("/api/v1/evidence/jobs/reconcile/latest", capturedUri);
            Assert.NotNull(job);
            Assert.Equal(jobId, job!.Id);
            Assert.Equal(EvidenceValidationJobKind.Reconcile, job.Kind);
            Assert.Equal(EvidenceValidationJobState.Failed, job.State);
            Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), job.StartedUtc);
            Assert.Equal(new DateTime(2026, 1, 2, 3, 9, 5, DateTimeKind.Utc), job.CompletedUtc);
            Assert.Equal(2, job.Log.Count);
            Assert.Null(job.IntegrityResults);
            Assert.NotNull(job.Discrepancies);
            Assert.Equal("event-456", job.Discrepancies![0].ProviderEventId);
            Assert.Equal("provider timeout", job.ErrorMessage);
        }

        [Fact]
        public async Task GetLatestAsync_On404_ReturnsNull()
        {
            var handler = new CaptureHttpMessageHandler(async (request, ct) =>
                new HttpResponseMessage(HttpStatusCode.NotFound));

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
            var jobs = new RemoteEvidenceValidationJobs(httpClient);

            EvidenceValidationJob? job = await jobs.GetLatestAsync(EvidenceValidationJobKind.Integrity, CancellationToken.None);

            Assert.Null(job);
        }

        private class CaptureHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public CaptureHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await _handler(request, cancellationToken);
            }
        }
    }
}
