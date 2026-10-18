namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using ValidateEvidencePage = VideoForensics.Ui.Shared.Pages.ValidateEvidence;

/// <summary>
/// Validate Evidence runs as server-side jobs; the page must reattach to the latest job and render its console and failure state.
/// </summary>
public class ValidateEvidencePageTests : OperatorSessionGateTestBase
{
    private readonly Mock<IEvidenceValidationJobs> _jobs = new(MockBehavior.Loose);

    public ValidateEvidencePageTests()
    {
        Services.AddScoped(_ => _jobs.Object);
    }

    [Fact]
    public async Task ValidateEvidence_WhenJobRunning_ShowsConsoleLines()
    {
        _jobs.Setup(m => m.GetLatestAsync(EvidenceValidationJobKind.Integrity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Job(EvidenceValidationJobState.Running, log: new[] { "12:00:01 Hashing a.mp4", "12:00:02 Hashing b.mp4" }));
        await SignInOperatorAsync();

        var component = Render<ValidateEvidencePage>();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("12:00:01 Hashing a.mp4", component.Markup);
            Assert.Contains("12:00:02 Hashing b.mp4", component.Markup);
        });
    }

    [Fact]
    public async Task ValidateEvidence_WhenJobFailed_ShowsErrorMessage()
    {
        _jobs.Setup(m => m.GetLatestAsync(EvidenceValidationJobKind.Integrity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Job(EvidenceValidationJobState.Failed, error: "Hash store unavailable"));
        await SignInOperatorAsync();

        var component = Render<ValidateEvidencePage>();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("Hash store unavailable", component.Markup);
            Assert.Contains("alert-danger", component.Markup);
        });
    }

    private static EvidenceValidationJob Job(
        EvidenceValidationJobState state,
        string[]? log = null,
        string? error = null) => new()
    {
        Id = Guid.NewGuid(),
        Kind = EvidenceValidationJobKind.Integrity,
        State = state,
        StartedUtc = DateTime.UtcNow,
        Log = log ?? Array.Empty<string>(),
        ErrorMessage = error,
    };
}
