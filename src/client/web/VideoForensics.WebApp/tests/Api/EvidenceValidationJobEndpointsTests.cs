
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api;
/// <summary>
/// Tests for the evidence validation job endpoints: start integrity, start reconcile, and latest-by-kind lookup.
/// </summary>
public class EvidenceValidationJobEndpointsTests
{
    private readonly Mock<IEvidenceValidationJobs> _jobs = new();

    private static EvidenceValidationJob BuildJob(Guid id, EvidenceValidationJobKind kind)
    {
        return new()
        {
            Id = id,
            Kind = kind,
            State = EvidenceValidationJobState.Running,
            StartedUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Log = new[] { "03:04:05 started" },
        };
    }

    [Fact]
    public async Task StartIntegrityJob_WithDeviceId_ReturnsOkWithJobDto()
    {
        var jobId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        _ = _jobs.Setup(j => j.StartIntegrityAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildJob(jobId, EvidenceValidationJobKind.Integrity));

        IResult result = await EvidenceEndpoints.StartIntegrityJobAsync(
            new StartIntegrityJobRequest(deviceId), _jobs.Object, CancellationToken.None);

        Ok<EvidenceValidationJobDto> ok = Assert.IsType<Ok<EvidenceValidationJobDto>>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(jobId, ok.Value!.Id);
        Assert.Equal("Integrity", ok.Value.Kind);
        Assert.Equal("Running", ok.Value.State);
        Assert.Equal(new[] { "03:04:05 started" }, ok.Value.Log);
    }

    [Fact]
    public async Task StartReconcileJob_WithRequest_ReturnsOkWithJobDtoAndForwardsParameters()
    {
        var jobId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        DateTime from = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc);
        _ = _jobs.Setup(j => j.StartReconcileAsync(deviceId, "ring-device-1", from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildJob(jobId, EvidenceValidationJobKind.Reconcile));

        IResult result = await EvidenceEndpoints.StartReconcileJobAsync(
            new StartReconcileJobRequest(deviceId, "ring-device-1", from, to), _jobs.Object, CancellationToken.None);

        Ok<EvidenceValidationJobDto> ok = Assert.IsType<Ok<EvidenceValidationJobDto>>(result);
        Assert.Equal(jobId, ok.Value!.Id);
        Assert.Equal("Reconcile", ok.Value.Kind);
        _jobs.Verify(j => j.StartReconcileAsync(deviceId, "ring-device-1", from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLatestJob_WhenNoJobExists_ReturnsNotFound()
    {
        _ = _jobs.Setup(j => j.GetLatestAsync(EvidenceValidationJobKind.Integrity, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvidenceValidationJob?)null);

        IResult result = await EvidenceEndpoints.GetLatestJobAsync("integrity", _jobs.Object, CancellationToken.None);

        _ = Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetLatestJob_WithUnknownKind_ReturnsBadRequestWithoutQueryingJobs()
    {
        IResult result = await EvidenceEndpoints.GetLatestJobAsync("bogus", _jobs.Object, CancellationToken.None);

        _ = Assert.IsType<BadRequest<string>>(result);
        _jobs.Verify(j => j.GetLatestAsync(It.IsAny<EvidenceValidationJobKind>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLatestJob_WithExistingJob_ReturnsOkWithJobDto()
    {
        var jobId = Guid.NewGuid();
        _ = _jobs.Setup(j => j.GetLatestAsync(EvidenceValidationJobKind.Reconcile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildJob(jobId, EvidenceValidationJobKind.Reconcile));

        IResult result = await EvidenceEndpoints.GetLatestJobAsync("reconcile", _jobs.Object, CancellationToken.None);

        Ok<EvidenceValidationJobDto> ok = Assert.IsType<Ok<EvidenceValidationJobDto>>(result);
        Assert.Equal(jobId, ok.Value!.Id);
    }
}
