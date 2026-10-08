using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class JammingEndpointTests
    {
        private static readonly Guid DeviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid OperatorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private static Device SampleDevice() => new() { Id = DeviceId, ProviderDeviceId = "p", Name = "Front Door", Type = "doorbell" };

        private static UpsertJammingIncidentRequest ValidRequest(Guid? id = null) => new(
            id ?? Guid.Empty, DeviceId,
            new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc),
            3, 14.5, "High", "note");

        private static HttpContext AdminContext()
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(VideoForensicsClaimTypes.OperatorId, OperatorId.ToString())
            ], "test"));
            return context;
        }

        private sealed class Fixture
        {
            public Mock<IJammingRepository> Jamming { get; } = new();
            public Mock<IDeviceRepository> Devices { get; } = new();
            public Mock<ISecurityAuditLogger> Audit { get; } = new();
            public Mock<INetworkTierResolver> Tier { get; } = new();
            public List<JammingIncidentRecord> Upserted { get; } = [];

            public Fixture(bool deviceExists = true)
            {
                _ = Devices.Setup(d => d.GetAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(deviceExists ? SampleDevice() : null);
                _ = Tier.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
                _ = Jamming.Setup(j => j.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((JammingIncidentRecord r, CancellationToken _) =>
                    {
                        if (r.Id == Guid.Empty)
                        {
                            r.Id = Guid.NewGuid();
                        }

                        Upserted.Add(r);
                        return r;
                    });
            }

            public Task<IResult> Upsert(UpsertJammingIncidentRequest request)
                => JammingEndpoints.UpsertIncidentAsync(request, Jamming.Object, Devices.Object, Audit.Object, Tier.Object, AdminContext(), CancellationToken.None);
        }

        private static int Status(IResult result) => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? 0;

        // ---- reads ---------------------------------------------------------------------------------

        [Fact]
        public async Task ListIncidents_PassesFiltersThroughAndMapsToDtos()
        {
            var jamming = new Mock<IJammingRepository>();
            DateTime from = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime to = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);
            _ = jamming.Setup(j => j.ListIncidentsAsync(DeviceId, from, to, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new JammingIncidentRecord { Id = Guid.NewGuid(), DeviceId = DeviceId, Confidence = JammingConfidenceLevel.Low, CaseId = Guid.NewGuid() }]);

            IResult result = await JammingEndpoints.ListIncidentsAsync(DeviceId, from, to, jamming.Object, CancellationToken.None);

            var ok = Assert.IsType<Ok<List<JammingIncidentDto>>>(result);
            JammingIncidentDto dto = Assert.Single(ok.Value!);
            Assert.Equal("Low", dto.Confidence);
            Assert.NotNull(dto.CaseId);
            jamming.Verify(j => j.ListIncidentsAsync(DeviceId, from, to, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ListIncidents_LocalKindDates_AreConvertedToUtcBeforeQuerying()
        {
            var jamming = new Mock<IJammingRepository>();
            _ = jamming.Setup(j => j.ListIncidentsAsync(null, It.IsAny<DateTime?>(), null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
            DateTime utc = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
            DateTime local = utc.ToLocalTime();

            _ = await JammingEndpoints.ListIncidentsAsync(null, local, null, jamming.Object, CancellationToken.None);

            jamming.Verify(j => j.ListIncidentsAsync(null, It.Is<DateTime?>(d => d == utc && d.Value.Kind == DateTimeKind.Utc), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetIncident_Found_ReturnsDtoWithCaseId()
        {
            var id = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var jamming = new Mock<IJammingRepository>();
            _ = jamming.Setup(j => j.GetIncidentAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingIncidentRecord { Id = id, DeviceId = DeviceId, CaseId = caseId });

            IResult result = await JammingEndpoints.GetIncidentAsync(id, jamming.Object, CancellationToken.None);

            Assert.Equal(caseId, Assert.IsType<Ok<JammingIncidentDto>>(result).Value!.CaseId);
        }

        [Fact]
        public async Task GetIncident_Missing_Returns404()
        {
            var jamming = new Mock<IJammingRepository>();

            IResult result = await JammingEndpoints.GetIncidentAsync(Guid.NewGuid(), jamming.Object, CancellationToken.None);

            Assert.Equal(404, Status(result));
        }

        [Fact]
        public async Task GetStats_Found_ReturnsDto()
        {
            var jamming = new Mock<IJammingRepository>();
            _ = jamming.Setup(j => j.GetStatsAsync(DeviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingStatsSummary { DeviceId = DeviceId, IncidentCount = 4 });

            IResult result = await JammingEndpoints.GetStatsAsync(DeviceId, jamming.Object, CancellationToken.None);

            Assert.Equal(4, Assert.IsType<Ok<JammingStatsSummaryDto>>(result).Value!.IncidentCount);
        }

        [Fact]
        public async Task GetStats_None_Returns404()
        {
            var jamming = new Mock<IJammingRepository>();

            IResult result = await JammingEndpoints.GetStatsAsync(DeviceId, jamming.Object, CancellationToken.None);

            Assert.Equal(404, Status(result));
        }

        [Fact]
        public async Task ListStats_ReturnsAllDtos()
        {
            var jamming = new Mock<IJammingRepository>();
            _ = jamming.Setup(j => j.ListStatsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new JammingStatsSummary { DeviceId = DeviceId }, new JammingStatsSummary { DeviceId = Guid.NewGuid() }]);

            IResult result = await JammingEndpoints.ListStatsAsync(jamming.Object, CancellationToken.None);

            Assert.Equal(2, Assert.IsType<Ok<List<JammingStatsSummaryDto>>>(result).Value!.Count);
        }

        // ---- recompute -----------------------------------------------------------------------------

        [Fact]
        public async Task Recompute_KnownDevice_RecomputesAndReturnsDto()
        {
            var f = new Fixture();
            _ = f.Jamming.Setup(j => j.RecomputeStatsAsync(DeviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingStatsSummary { DeviceId = DeviceId, IncidentCount = 2 });

            IResult result = await JammingEndpoints.RecomputeStatsAsync(DeviceId, f.Jamming.Object, f.Devices.Object, CancellationToken.None);

            Assert.Equal(2, Assert.IsType<Ok<JammingStatsSummaryDto>>(result).Value!.IncidentCount);
        }

        [Fact]
        public async Task Recompute_UnknownDevice_Returns404WithoutTouchingRepository()
        {
            var f = new Fixture(deviceExists: false);

            IResult result = await JammingEndpoints.RecomputeStatsAsync(DeviceId, f.Jamming.Object, f.Devices.Object, CancellationToken.None);

            Assert.Equal(404, Status(result));
            f.Jamming.Verify(j => j.RecomputeStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---- upsert: validation --------------------------------------------------------------------

        public static TheoryData<string, UpsertJammingIncidentRequest> InvalidRequests()
        {
            UpsertJammingIncidentRequest ok = new(Guid.Empty, DeviceId,
                new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc), 3, 14.5, "High", "note");
            return new TheoryData<string, UpsertJammingIncidentRequest>
            {
                { "start equals end", ok with { EndUtc = ok.StartUtc } },
                { "start after end", ok with { StartUtc = ok.EndUtc.AddMinutes(1) } },
                { "negative degradation", ok with { AverageDegradationDb = -0.1 } },
                { "NaN degradation", ok with { AverageDegradationDb = double.NaN } },
                { "infinite degradation", ok with { AverageDegradationDb = double.PositiveInfinity } },
                { "negative event count", ok with { AffectedEventCount = -1 } },
                { "unknown confidence name", ok with { Confidence = "Bogus" } },
                { "out-of-range numeric confidence", ok with { Confidence = "99" } },
                { "null confidence", ok with { Confidence = null! } },
                { "notes too long", ok with { Notes = new string('x', 2001) } }
            };
        }

        [Theory]
        [MemberData(nameof(InvalidRequests))]
        public async Task Upsert_InvalidRequest_Returns400AndSavesNothing(string _, UpsertJammingIncidentRequest request)
        {
            var f = new Fixture();

            IResult result = await f.Upsert(request);

            Assert.Equal(400, Status(result));
            f.Jamming.Verify(j => j.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Never);
            f.Audit.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Upsert_NotesExactlyAtLimit_IsAccepted()
        {
            var f = new Fixture();

            IResult result = await f.Upsert(ValidRequest() with { Notes = new string('x', 2000) });

            Assert.Equal(200, Status(result));
        }

        [Fact]
        public async Task Upsert_ZeroDegradationAndZeroEvents_AreAccepted()
        {
            var f = new Fixture();

            IResult result = await f.Upsert(ValidRequest() with { AverageDegradationDb = 0, AffectedEventCount = 0 });

            Assert.Equal(200, Status(result));
        }

        [Fact]
        public async Task Upsert_LowerCaseConfidenceName_IsAcceptedNotAServerError()
        {
            var f = new Fixture();

            IResult result = await f.Upsert(ValidRequest() with { Confidence = "high" });

            Assert.Equal(200, Status(result));
            Assert.Equal(JammingConfidenceLevel.High, Assert.Single(f.Upserted).Confidence);
        }

        [Fact]
        public async Task Upsert_UnknownDevice_Returns404NotAServerError()
        {
            var f = new Fixture(deviceExists: false);

            IResult result = await f.Upsert(ValidRequest());

            Assert.Equal(404, Status(result));
            f.Jamming.Verify(j => j.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Never);
            f.Audit.VerifyNoOtherCalls();
        }

        // ---- upsert: server-controlled fields ------------------------------------------------------

        [Fact]
        public async Task Upsert_NewIncident_ForcesManualSourceAndServerTimestampAndNoCaseId()
        {
            var f = new Fixture();
            DateTime before = DateTime.UtcNow;

            IResult result = await f.Upsert(ValidRequest());

            var ok = Assert.IsType<Ok<JammingIncidentDto>>(result);
            JammingIncidentRecord sent = Assert.Single(f.Upserted);
            Assert.Equal(JammingIncidentSource.ManuallyRecorded, sent.Source);
            Assert.Null(sent.CaseId);
            Assert.InRange(sent.DetectedAtUtc, before, DateTime.UtcNow);
            Assert.Equal(DateTimeKind.Utc, sent.DetectedAtUtc.Kind);
            Assert.Equal("ManuallyRecorded", ok.Value!.Source);
            Assert.Equal(sent.Id, ok.Value.Id);
            Assert.NotEqual(Guid.Empty, ok.Value.Id);
            f.Jamming.Verify(j => j.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Upsert_NewIncidentWithAutoDetectedSource_StoresAutoDetected()
        {
            var f = new Fixture();

            IResult result = await f.Upsert(ValidRequest() with { Source = "AutoDetected" });

            Assert.Equal(JammingIncidentSource.AutoDetected, Assert.Single(f.Upserted).Source);
            Assert.Equal("AutoDetected", Assert.IsType<Ok<JammingIncidentDto>>(result).Value!.Source);
        }

        [Fact]
        public async Task Upsert_NewIncidentWithLowerCaseSource_IsAccepted()
        {
            var f = new Fixture();

            IResult result = await f.Upsert(ValidRequest() with { Source = "autodetected" });

            Assert.Equal(200, Status(result));
            Assert.Equal(JammingIncidentSource.AutoDetected, Assert.Single(f.Upserted).Source);
        }

        [Fact]
        public async Task Upsert_NewIncidentWithOmittedSource_StoresManuallyRecorded()
        {
            var f = new Fixture();

            _ = await f.Upsert(ValidRequest() with { Source = null });

            Assert.Equal(JammingIncidentSource.ManuallyRecorded, Assert.Single(f.Upserted).Source);
        }

        [Theory]
        [InlineData("Bogus")]
        [InlineData("99")]
        [InlineData("")]
        public async Task Upsert_InvalidSource_Returns400AndSavesNothing(string source)
        {
            var f = new Fixture();

            IResult result = await f.Upsert(ValidRequest() with { Source = source });

            Assert.Equal(400, Status(result));
            f.Jamming.Verify(j => j.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Never);
            f.Audit.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Upsert_ExistingIncidentWithDifferentSourceClaim_KeepsStoredSource()
        {
            var f = new Fixture();
            var id = Guid.NewGuid();
            _ = f.Jamming.Setup(j => j.GetIncidentAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new JammingIncidentRecord
            {
                Id = id, DeviceId = DeviceId, Source = JammingIncidentSource.ManuallyRecorded, Confidence = JammingConfidenceLevel.Low
            });

            _ = await f.Upsert(ValidRequest(id) with { Source = "AutoDetected" });

            Assert.Equal(JammingIncidentSource.ManuallyRecorded, Assert.Single(f.Upserted).Source);
        }

        [Fact]
        public async Task Upsert_NewIncident_ReturnsCaseIdTheRepositoryAssignedAfterSave()
        {
            var f = new Fixture();
            var caseId = Guid.NewGuid();
            // After the insert the repository auto-creates the case; a fresh read shows it.
            _ = f.Jamming.Setup(j => j.GetIncidentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => f.Upserted.Count == 0 ? null : new JammingIncidentRecord
                {
                    Id = id, DeviceId = DeviceId, StartUtc = f.Upserted[0].StartUtc, EndUtc = f.Upserted[0].EndUtc,
                    Confidence = JammingConfidenceLevel.High, Source = JammingIncidentSource.ManuallyRecorded, CaseId = caseId
                });

            IResult result = await f.Upsert(ValidRequest());

            Assert.Equal(caseId, Assert.IsType<Ok<JammingIncidentDto>>(result).Value!.CaseId);
        }

        [Fact]
        public async Task Upsert_NewIncidentWithClientChosenUnknownId_KeepsThatId()
        {
            var f = new Fixture();
            var id = Guid.NewGuid();

            _ = await f.Upsert(ValidRequest(id));

            Assert.Equal(id, Assert.Single(f.Upserted).Id);
            Assert.Equal(JammingIncidentSource.ManuallyRecorded, f.Upserted[0].Source);
        }

        [Fact]
        public async Task Upsert_ExistingIncident_KeepsStoredSourceDetectedAtAndCaseId()
        {
            var f = new Fixture();
            var id = Guid.NewGuid();
            var storedCase = Guid.NewGuid();
            DateTime storedDetected = new(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);
            _ = f.Jamming.Setup(j => j.GetIncidentAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new JammingIncidentRecord
            {
                Id = id, DeviceId = DeviceId, Source = JammingIncidentSource.AutoDetected, DetectedAtUtc = storedDetected, CaseId = storedCase,
                Confidence = JammingConfidenceLevel.Low
            });

            IResult result = await f.Upsert(ValidRequest(id) with { Confidence = "Definite", Notes = "edited" });

            JammingIncidentRecord sent = Assert.Single(f.Upserted);
            Assert.Equal(JammingIncidentSource.AutoDetected, sent.Source);
            Assert.Equal(storedDetected, sent.DetectedAtUtc);
            Assert.Equal(storedCase, sent.CaseId);
            Assert.Equal(JammingConfidenceLevel.Definite, sent.Confidence);
            Assert.Equal("edited", sent.Notes);
            var dto = Assert.IsType<Ok<JammingIncidentDto>>(result).Value!;
            Assert.Equal("AutoDetected", dto.Source);
            Assert.Equal(storedCase, dto.CaseId);
        }

        // ---- upsert: audit -------------------------------------------------------------------------

        [Fact]
        public async Task Upsert_Success_WritesOneAuditEntryWithDeviceNameAndConfidenceButNoIds()
        {
            var f = new Fixture();

            _ = await f.Upsert(ValidRequest());

            f.Audit.Verify(a => a.LogAsync(
                SecurityAuditEventTypes.JammingIncidentRecorded,
                OperatorId, null, "127.0.0.1",
                It.Is<string>(d => d.Contains("Front Door") && d.Contains("High") && !d.Contains(DeviceId.ToString())),
                false, It.IsAny<CancellationToken>()), Times.Once);
            f.Audit.VerifyNoOtherCalls();
        }
    }
}