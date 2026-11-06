using AngleSharp.Dom;

using Bunit;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The Case section at the top of the media right sidebar: pick an open case to enter it, or leave it.</summary>
public class ReviewCaseSection_Tests : ReviewTestContext
{
    private ForensicCase _openCase = null!;

    /// <summary>Gives the context one open case the picker can list.</summary>
    private void ArrangeOpenCase()
    {
        _openCase = PendingTagFakes.CreateOpenCase();
        Mock<ICaseRepository> repository = PendingTagFakes.CreateRepository(_openCase);
        _ = repository
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ForensicCase> { _openCase });
        CaseRepositoryDouble = repository.Object;
    }

    private IRenderedComponent<ReviewCaseSection> RenderSection()
    {
        return Render<ReviewCaseSection>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.Devices, Array.Empty<Guid>()));
    }

    [Fact]
    public void ReviewCaseSection_Rendered_ListsNoCaseAndTheOpenCases()
    {
        // Arrange
        ArrangeOpenCase();

        // Act
        IRenderedComponent<ReviewCaseSection> cut = RenderSection();

        // Assert
        IElement[] options = cut.FindAll("[data-testid='review-case-picker'] option").ToArray();
        Assert.Equal(2, options.Length);
        Assert.Equal(string.Empty, options[0].GetAttribute("value"));
        Assert.Equal(_openCase.Id.ToString(), options[1].GetAttribute("value"));
        Assert.Contains(_openCase.Title, options[1].TextContent);
    }

    [Fact]
    public void ReviewCaseSection_PickACase_EntersTheCase()
    {
        // Arrange
        ArrangeOpenCase();
        IRenderedComponent<ReviewCaseSection> cut = RenderSection();

        // Act
        cut.Find("[data-testid='review-case-picker']").Change(_openCase.Id.ToString());

        // Assert
        Assert.True(CaseMode.Active);
        Assert.Equal(_openCase.Id, CaseMode.CaseId);
    }

    [Fact]
    public void ReviewCaseSection_PickNoCase_LeavesTheCase()
    {
        // Arrange
        ArrangeOpenCase();
        CaseMode.EnterAsync(_openCase.Id, CancellationToken.None).GetAwaiter().GetResult();
        IRenderedComponent<ReviewCaseSection> cut = RenderSection();

        // Act
        cut.Find("[data-testid='review-case-picker']").Change(string.Empty);

        // Assert
        Assert.False(CaseMode.Active);
    }

    [Fact]
    public void ReviewCaseSection_CaseAlreadyEntered_SelectsItsOption()
    {
        // Arrange
        ArrangeOpenCase();
        CaseMode.EnterAsync(_openCase.Id, CancellationToken.None).GetAwaiter().GetResult();

        // Act
        IRenderedComponent<ReviewCaseSection> cut = RenderSection();

        // Assert
        Assert.True(cut.Find($"option[value='{_openCase.Id}']").HasAttribute("selected"));
        Assert.False(cut.Find("option[value='']").HasAttribute("selected"));
    }
}
