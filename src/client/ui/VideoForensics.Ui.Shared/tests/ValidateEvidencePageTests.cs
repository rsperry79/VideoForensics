namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Ui.Shared.Resources;
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
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);
        Services.AddSingleton<IStringLocalizer<SharedResources>>(TestLocalizer.Create());
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

    [Fact]
    public void ValidateEvidence_WhenRendered_ShowsBackControlWithLocalizedLabel()
    {
        var component = Render<ValidateEvidencePage>();

        var analyzeLabel = Services.GetRequiredService<IStringLocalizer<ValidateEvidencePage>>()["BreadcrumbAnalyze"].Value;
        var backTitle = TestLocalizer.Create()["BreadcrumbBack"].Value;

        var link = Assert.Single(component.FindAll("nav a"));
        Assert.Equal("/analyze", link.GetAttribute("href"));
        Assert.Equal(analyzeLabel, link.TextContent.Trim());
        Assert.NotNull(component.Find($"button[title='{backTitle}']"));
    }

    [Fact]
    public void ValidateEvidence_WhenBackClicked_NavigatesToAnalyze()
    {
        var component = Render<ValidateEvidencePage>();
        var backTitle = TestLocalizer.Create()["BreadcrumbBack"].Value;

        component.Find($"button[title='{backTitle}']").Click();

        Assert.EndsWith("/analyze", Services.GetRequiredService<NavigationManager>().Uri);
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
