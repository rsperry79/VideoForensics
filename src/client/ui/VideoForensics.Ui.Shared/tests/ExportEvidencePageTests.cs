
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>The export page is attached to a case: it exports that case's reports and evidence, never an unscoped set.</summary>
public class ExportEvidencePageTests : CasesPagesTestBase
{
    private readonly Mock<IEvidenceExportService> _exportMock = new();
    private readonly ForensicCase _caseA = CreateTestCase(caseNumber: "CASE-A", title: "Alpha");
    private readonly ForensicCase _caseB = CreateTestCase(caseNumber: "CASE-B", title: "Bravo");

    public ExportEvidencePageTests()
    {
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<CaseStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_caseA, _caseB]);
        _ = _exportMock
            .Setup(e => e.ExportEvidenceAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new ExportResult { Success = true, ArchivePath = "a.zip", ItemsIncluded = 1 });
        _ = Services.AddScoped(_ => _exportMock.Object);

        var webAuthn = new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>());
        _ = webAuthn.Setup(w => w.StepUpAsync(It.IsAny<string>())).ReturnsAsync("step-token");
        _ = Services.AddScoped(_ => webAuthn.Object);
        _ = Services.AddScoped<StepUpService>();
    }

    private async Task<IRenderedComponent<ExportEvidence>> RenderPageAsync(Guid? caseId)
    {
        PairedSessionState session = await RegisterRoleAsync(OperatorRole.SuperAdmin);
        session.SetServerSessionKind(localTier: true, passkeySession: true, passwordSession: false);
        // The page reads ?caseId= from the URL, so navigate before rendering.
        NavigationManager nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.NavigateTo(caseId.HasValue ? $"/review/export?caseId={caseId}" : "/review/export");
        return Render<ExportEvidence>();
    }

    [Fact]
    public async Task Page_CaseIdQuery_PreselectsThatCase()
    {
        IRenderedComponent<ExportEvidence> cut = await RenderPageAsync(_caseB.Id);

        Assert.Equal(_caseB.Id.ToString(), cut.Find("[data-testid='case-picker'] option[selected]").GetAttribute("value"));
        Assert.False(cut.Find("[data-testid='export-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Page_ListsCasesInPicker()
    {
        IRenderedComponent<ExportEvidence> cut = await RenderPageAsync(null);

        string[] values = cut.FindAll("[data-testid='case-picker'] option").Select(o => o.GetAttribute("value") ?? string.Empty).ToArray();
        Assert.Contains(_caseA.Id.ToString(), values);
        Assert.Contains(_caseB.Id.ToString(), values);
    }

    [Fact]
    public async Task Page_NoCaseSelected_DisablesExport()
    {
        IRenderedComponent<ExportEvidence> cut = await RenderPageAsync(null);

        Assert.True(cut.Find("[data-testid='export-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Page_UnknownCaseIdQuery_DisablesExport()
    {
        IRenderedComponent<ExportEvidence> cut = await RenderPageAsync(Guid.NewGuid());

        Assert.True(cut.Find("[data-testid='export-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Page_PickingACase_EnablesExport()
    {
        IRenderedComponent<ExportEvidence> cut = await RenderPageAsync(null);

        cut.Find("[data-testid='case-picker']").Change(_caseA.Id.ToString());

        Assert.False(cut.Find("[data-testid='export-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Page_Export_SendsSelectedCaseId()
    {
        IRenderedComponent<ExportEvidence> cut = await RenderPageAsync(_caseA.Id);

        await cut.Find("[data-testid='export-button']").ClickAsync();

        _exportMock.Verify(e => e.ExportEvidenceAsync(_caseA.Id, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), "step-token"), Times.Once);
    }

    [Fact]
    public async Task Page_ExportAfterChangingPickerCase_SendsNewCaseId()
    {
        IRenderedComponent<ExportEvidence> cut = await RenderPageAsync(_caseA.Id);
        cut.Find("[data-testid='case-picker']").Change(_caseB.Id.ToString());

        await cut.Find("[data-testid='export-button']").ClickAsync();

        _exportMock.Verify(e => e.ExportEvidenceAsync(_caseB.Id, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
        _exportMock.Verify(e => e.ExportEvidenceAsync(_caseA.Id, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }
}
