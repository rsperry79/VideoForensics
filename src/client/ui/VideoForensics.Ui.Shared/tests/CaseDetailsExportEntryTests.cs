namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;

/// <summary>The case page links to the case-scoped export page.</summary>
public class CaseDetailsExportEntryTests : CasesPagesTestBase
{
    public CaseDetailsExportEntryTests()
    {
        RegisterGridStub<CaseDetails.CaseItemRow>();
    }

    [Fact]
    public void CaseDetails_ExportCaseButton_NavigatesToExportPageWithCaseId()
    {
        Guid caseId = Guid.NewGuid();
        _ = CaseRepositoryMock
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestCase(id: caseId));
        var cut = Render<CaseDetails>(p => p.Add(c => c.CaseId, caseId));

        cut.Find("[data-testid='export-case']").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/review/export?caseId={caseId}", nav.Uri);
    }
}
