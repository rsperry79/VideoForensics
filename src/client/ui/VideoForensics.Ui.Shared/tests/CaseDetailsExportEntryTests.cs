
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>The case page links to the case-scoped export page.</summary>
public class CaseDetailsExportEntryTests : CasesPagesTestBase
{
    public CaseDetailsExportEntryTests()
    {
        RegisterGridStub<CaseDetails.CaseItemRow>();
        _ = Services.AddScoped<RightPanelContentService>();
    }

    [Fact]
    public void CaseDetails_ExportCaseButton_NavigatesToExportPageWithCaseId()
    {
        var caseId = Guid.NewGuid();
        _ = CaseRepositoryMock
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestCase(id: caseId));
        IRenderedComponent<CaseDetails> cut = Render<CaseDetails>(p => p.Add(c => c.CaseId, caseId));

        cut.Find("[data-testid='export-case']").Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/review/export?caseId={caseId}", nav.Uri);
    }
}
