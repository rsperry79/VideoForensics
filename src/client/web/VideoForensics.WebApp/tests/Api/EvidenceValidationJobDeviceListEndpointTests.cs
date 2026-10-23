
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api;
/// <summary>A device-list integrity request starts one job over those devices; a single-device request is unchanged.</summary>
public class EvidenceValidationJobDeviceListEndpointTests
{
    private readonly Mock<IEvidenceValidationJobs> _jobs = new();

    private static EvidenceValidationJob BuildJob(Guid id)
    {
        return new()
        {
            Id = id,
            Kind = EvidenceValidationJobKind.Integrity,
            State = EvidenceValidationJobState.Running,
            StartedUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Log = new[] { "03:04:05 started" },
        };
    }

    [Fact]
    public async Task StartIntegrityJob_WithDeviceIds_StartsOneJobOverThoseDevices()
    {
        var jobId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _ = _jobs.Setup(j => j.StartIntegrityForDevicesAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { first, second })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildJob(jobId));

        IResult result = await EvidenceEndpoints.StartIntegrityJobAsync(
            new StartIntegrityJobRequest(null, new[] { first, second }), _jobs.Object, CancellationToken.None);

        Ok<EvidenceValidationJobDto> ok = Assert.IsType<Ok<EvidenceValidationJobDto>>(result);
        Assert.Equal(jobId, ok.Value!.Id);
        _jobs.Verify(j => j.StartIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartIntegrityJob_WithEmptyDeviceIds_FallsBackToSingleDeviceForm()
    {
        var jobId = Guid.NewGuid();
        _ = _jobs.Setup(j => j.StartIntegrityAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildJob(jobId));

        IResult result = await EvidenceEndpoints.StartIntegrityJobAsync(
            new StartIntegrityJobRequest(null, Array.Empty<Guid>()), _jobs.Object, CancellationToken.None);

        _ = Assert.IsType<Ok<EvidenceValidationJobDto>>(result);
        _jobs.Verify(j => j.StartIntegrityForDevicesAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
