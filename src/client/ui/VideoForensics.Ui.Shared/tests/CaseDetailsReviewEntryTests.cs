using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The case page's "Review selected evidence" entry point to the review page's case view.</summary>
public class CaseDetailsReviewEntry_Tests : CasesPagesTestBase
{
    public CaseDetailsReviewEntry_Tests()
    {
        RegisterGridStub<CaseDetails.CaseItemRow>();
        _ = Services.AddScoped<RightPanelContentService>();
    }

    [Fact]
    public void CaseDetails_ReviewSelectedEvidence_WithPinnedItems_NavigatesToMediaWithTheCaseId()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        MediaItem media = CreateTestMediaItem();
        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateTestCase(id: caseId));
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateMediaPin(caseId, media.Id, media.Sha256Hash!) });
        _ = MediaItemRepositoryMock.Setup(r => r.GetAsync(media.Id, It.IsAny<CancellationToken>())).ReturnsAsync(media);
        IRenderedComponent<CaseDetails> cut = Render<CaseDetails>(p => p.Add(c => c.CaseId, caseId));

        // Act
        cut.Find("[data-testid='review-selected-evidence']").Click();

        // Assert
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/media?reviewcase={caseId:D}", nav.Uri);
    }

    [Fact]
    public void CaseDetails_ReviewSelectedEvidence_WithNoPinnedItems_IsDisabled()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateTestCase(id: caseId));
        IRenderedComponent<CaseDetails> cut = Render<CaseDetails>(p => p.Add(c => c.CaseId, caseId));

        // Act
        AngleSharp.Dom.IElement button = cut.Find("[data-testid='review-selected-evidence']");

        // Assert
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void CaseDetails_ReviewSelectedEvidence_WithOnlyRemovedPins_IsDisabled()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CaseItem removed = CreateEventPin(caseId, Guid.NewGuid());
        removed.RemovedAtUtc = FixedNowUtc;
        removed.RemovedBy = "op";
        removed.RemovalReason = "wrong case";
        _ = CaseRepositoryMock.Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateTestCase(id: caseId));
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { removed });
        IRenderedComponent<CaseDetails> cut = Render<CaseDetails>(p => p.Add(c => c.CaseId, caseId));

        // Act
        AngleSharp.Dom.IElement button = cut.Find("[data-testid='review-selected-evidence']");

        // Assert
        Assert.True(button.HasAttribute("disabled"));
    }
}
