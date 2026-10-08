using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteJammingRepositoryTests
    {
        private static readonly Guid DeviceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid IncidentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid CaseId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static (RemoteJammingRepository Repo, List<HttpRequestMessage> Seen, List<string?> Bodies, List<CancellationToken> Tokens) Create(
            Func<HttpRequestMessage, HttpResponseMessage> respond, bool blockUntilCancelled = false)
        {
            var seen = new List<HttpRequestMessage>();
            var bodies = new List<string?>();
            var tokens = new List<CancellationToken>();
            var handler = new FakeHandler(async (req, ct) =>
            {
                seen.Add(req);
                tokens.Add(ct);
                if (blockUntilCancelled)
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }

                bodies.Add(req.Content == null ? null : await req.Content.ReadAsStringAsync(ct));
                return respond(req);
            });
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            return (new RemoteJammingRepository(http), seen, bodies, tokens);
        }

        private static JammingIncidentDto SampleIncidentDto(Guid? caseId = null) => new(
            IncidentId, DeviceId,
            new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc),
            4, 12.5, "High", new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc), "notes", "ManuallyRecorded", caseId);

        private static JammingStatsSummaryDto SampleStatsDto() => new(
            Guid.NewGuid(), DeviceId, 3, 90.5, 11.5, 20.0, 1, 1, 1, 0,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        private static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = JsonContent.Create(value) };

        [Fact]
        public async Task ListIncidentsAsync_NoFilters_GetsBareRouteAndMapsDtos()
        {
            var (repo, seen, _, _) = Create(_ => Json(new List<JammingIncidentDto> { SampleIncidentDto(CaseId) }));

            IReadOnlyList<JammingIncidentRecord> result = await repo.ListIncidentsAsync(null, null, null, CancellationToken.None);

            HttpRequestMessage req = Assert.Single(seen);
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("/api/v1/jamming/incidents", req.RequestUri!.AbsolutePath);
            Assert.Equal(string.Empty, req.RequestUri.Query);
            JammingIncidentRecord rec = Assert.Single(result);
            Assert.Equal(IncidentId, rec.Id);
            Assert.Equal(DeviceId, rec.DeviceId);
            Assert.Equal(JammingConfidenceLevel.High, rec.Confidence);
            Assert.Equal(JammingIncidentSource.ManuallyRecorded, rec.Source);
            Assert.Equal(CaseId, rec.CaseId);
            Assert.Equal(12.5, rec.AverageDegradationDb);
            Assert.Equal(4, rec.AffectedEventCount);
            Assert.Equal("notes", rec.Notes);
        }

        [Fact]
        public async Task ListIncidentsAsync_WithFilters_SendsDeviceAndRangeQuery()
        {
            var (repo, seen, _, _) = Create(_ => Json(new List<JammingIncidentDto>()));
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 2, 12, 30, 0, DateTimeKind.Utc);

            _ = await repo.ListIncidentsAsync(DeviceId, from, to, CancellationToken.None);

            var parsed = System.Web.HttpUtility.ParseQueryString(seen[0].RequestUri!.Query);
            Assert.Equal(DeviceId.ToString(), parsed["deviceId"]);
            Assert.Equal(from, DateTime.Parse(parsed["fromUtc"]!, null, System.Globalization.DateTimeStyles.RoundtripKind));
            Assert.Equal(to, DateTime.Parse(parsed["toUtc"]!, null, System.Globalization.DateTimeStyles.RoundtripKind));
            Assert.Equal(3, parsed.Count);
        }

        [Fact]
        public async Task ListIncidentsAsync_ForwardsCancellationTokenToHttpPipeline()
        {
            var (repo, _, _, _) = Create(_ => Json(new List<JammingIncidentDto>()), blockUntilCancelled: true);
            using var cts = new CancellationTokenSource();

            Task<IReadOnlyList<JammingIncidentRecord>> pending = repo.ListIncidentsAsync(null, null, null, cts.Token);
            cts.Cancel();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        [Fact]
        public async Task GetIncidentAsync_Found_MapsRecordIncludingCaseId()
        {
            var (repo, seen, _, _) = Create(_ => Json(SampleIncidentDto(CaseId)));

            JammingIncidentRecord? rec = await repo.GetIncidentAsync(IncidentId, CancellationToken.None);

            Assert.Equal($"/api/v1/jamming/incidents/{IncidentId}", seen[0].RequestUri!.AbsolutePath);
            Assert.NotNull(rec);
            Assert.Equal(CaseId, rec.CaseId);
            Assert.Equal(JammingConfidenceLevel.High, rec.Confidence);
        }

        [Fact]
        public async Task GetIncidentAsync_NotFound_ReturnsNull()
        {
            var (repo, _, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            JammingIncidentRecord? rec = await repo.GetIncidentAsync(IncidentId, CancellationToken.None);

            Assert.Null(rec);
        }

        [Fact]
        public async Task GetIncidentAsync_ServerError_Throws()
        {
            var (repo, _, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

            _ = await Assert.ThrowsAsync<HttpRequestException>(() => repo.GetIncidentAsync(IncidentId, CancellationToken.None));
        }

        [Fact]
        public async Task GetStatsAsync_Found_MapsSummary()
        {
            JammingStatsSummaryDto dto = SampleStatsDto();
            var (repo, seen, _, _) = Create(_ => Json(dto));

            JammingStatsSummary? stats = await repo.GetStatsAsync(DeviceId, CancellationToken.None);

            Assert.Equal(HttpMethod.Get, seen[0].Method);
            Assert.Equal($"/api/v1/jamming/stats/{DeviceId}", seen[0].RequestUri!.AbsolutePath);
            Assert.NotNull(stats);
            Assert.Equal(3, stats.IncidentCount);
            Assert.Equal(90.5, stats.TotalJammedDurationMinutes);
            Assert.Equal(dto.FirstIncidentUtc, stats.FirstIncidentUtc);
        }

        [Fact]
        public async Task GetStatsAsync_NotFound_ReturnsNull()
        {
            var (repo, _, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            Assert.Null(await repo.GetStatsAsync(DeviceId, CancellationToken.None));
        }

        [Fact]
        public async Task GetStatsAsync_ServerError_Throws()
        {
            var (repo, _, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

            _ = await Assert.ThrowsAsync<HttpRequestException>(() => repo.GetStatsAsync(DeviceId, CancellationToken.None));
        }

        [Fact]
        public async Task ListStatsAsync_GetsStatsRouteAndMapsAll()
        {
            var (repo, seen, _, _) = Create(_ => Json(new List<JammingStatsSummaryDto> { SampleStatsDto(), SampleStatsDto() }));

            IReadOnlyList<JammingStatsSummary> result = await repo.ListStatsAsync(CancellationToken.None);

            Assert.Equal(HttpMethod.Get, seen[0].Method);
            Assert.Equal("/api/v1/jamming/stats", seen[0].RequestUri!.AbsolutePath);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task RecomputeStatsAsync_PostsRecomputeRouteAndMapsSummary()
        {
            var (repo, seen, _, _) = Create(_ => Json(SampleStatsDto()));

            JammingStatsSummary stats = await repo.RecomputeStatsAsync(DeviceId, CancellationToken.None);

            Assert.Equal(HttpMethod.Post, seen[0].Method);
            Assert.Equal($"/api/v1/jamming/stats/{DeviceId}/recompute", seen[0].RequestUri!.AbsolutePath);
            Assert.Equal(3, stats.IncidentCount);
        }

        [Fact]
        public async Task RecomputeStatsAsync_NotFound_Throws()
        {
            var (repo, _, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            _ = await Assert.ThrowsAsync<HttpRequestException>(() => repo.RecomputeStatsAsync(DeviceId, CancellationToken.None));
        }

        [Fact]
        public async Task UpsertIncidentAsync_PutsRequestWithSourceButWithoutServerControlledFields_AndReturnsSavedRecord()
        {
            var (repo, seen, bodies, _) = Create(_ => Json(SampleIncidentDto(CaseId)));
            var incident = new JammingIncidentRecord
            {
                Id = IncidentId,
                DeviceId = DeviceId,
                StartUtc = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc),
                AffectedEventCount = 4,
                AverageDegradationDb = 12.5,
                Confidence = JammingConfidenceLevel.High,
                Notes = "notes",
                Source = JammingIncidentSource.AutoDetected,
                DetectedAtUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CaseId = Guid.NewGuid()
            };

            JammingIncidentRecord saved = await repo.UpsertIncidentAsync(incident, CancellationToken.None);

            Assert.Equal(HttpMethod.Put, seen[0].Method);
            Assert.Equal("/api/v1/jamming/incidents", seen[0].RequestUri!.AbsolutePath);
            using JsonDocument body = JsonDocument.Parse(bodies[0]!);
            JsonElement root = body.RootElement;
            Assert.False(root.EnumerateObject().Any(p => p.Name.Equals("caseId", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal("AutoDetected", root.GetProperty("source").GetString());
            Assert.False(root.EnumerateObject().Any(p => p.Name.Equals("detectedAtUtc", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(IncidentId, root.GetProperty("id").GetGuid());
            Assert.Equal(DeviceId, root.GetProperty("deviceId").GetGuid());
            Assert.Equal("High", root.GetProperty("confidence").GetString());
            Assert.Equal(4, root.GetProperty("affectedEventCount").GetInt32());
            Assert.Equal(12.5, root.GetProperty("averageDegradationDb").GetDouble());
            Assert.Equal("notes", root.GetProperty("notes").GetString());
            // The server's saved record (incl. its CaseId) is what the caller gets back.
            Assert.Equal(CaseId, saved.CaseId);
            Assert.Equal(JammingIncidentSource.ManuallyRecorded, saved.Source);
        }

        [Fact]
        public async Task UpsertIncidentAsync_BadRequest_Throws()
        {
            var (repo, _, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

            _ = await Assert.ThrowsAsync<HttpRequestException>(() => repo.UpsertIncidentAsync(new JammingIncidentRecord(), CancellationToken.None));
        }

        [Fact]
        public async Task UpsertIncidentAsync_Cancelled_PropagatesCancellation()
        {
            var (repo, _, _, _) = Create(_ => Json(SampleIncidentDto()));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.UpsertIncidentAsync(new JammingIncidentRecord(), cts.Token));
        }

        [Fact]
        public void AddVideoForensicsClientApi_ResolvesIJammingRepositoryToRemote()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(new Moq.Mock<Microsoft.JSInterop.IJSRuntime>().Object);
            services.AddSingleton<VideoForensics.Ui.Shared.Services.PairedSessionState>();
            services.AddVideoForensicsClientApi(new Uri("http://localhost:5000"));
            using ServiceProvider provider = services.BuildServiceProvider();

            IJammingRepository repo = provider.GetRequiredService<IJammingRepository>();

            Assert.IsType<RemoteJammingRepository>(repo);
        }

        [Fact]
        public void AddVideoForensicsClientApi_ResolvesJammingToolsOrchestrator()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(new Moq.Mock<Microsoft.JSInterop.IJSRuntime>().Object);
            services.AddSingleton<VideoForensics.Ui.Shared.Services.PairedSessionState>();
            services.AddVideoForensicsClientApi(new Uri("http://localhost:5000"));
            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();

            VideoForensics.Client.Core.Tools.JammingToolsOrchestrator orchestrator =
                scope.ServiceProvider.GetRequiredService<VideoForensics.Client.Core.Tools.JammingToolsOrchestrator>();

            Assert.NotNull(orchestrator);
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

            public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => _respond(request, cancellationToken);
        }
    }
}