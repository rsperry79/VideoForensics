using Bunit;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>The popup host: one backdrop and one Escape handler for every review popup, in the same container as the popups.</summary>
public class ReviewPopupHost_Tests : ReviewTestContext
{
    private IRenderedComponent<ReviewPopupHost> RenderHost()
    {
        return Render<ReviewPopupHost>(parameters => parameters
            .AddChildContent("<div class=\"popup-child\">popup</div>"));
    }

    [Fact]
    public void ReviewPopupHost_PopupOpen_RendersBackdrop()
    {
        // Arrange
        Services.GetRequiredService<ReviewPopupState>().Toggle(ReviewPopupState.Camera);

        // Act
        IRenderedComponent<ReviewPopupHost> cut = RenderHost();

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-popup-backdrop']"));
    }

    [Fact]
    public void ReviewPopupHost_NoPopupOpen_RendersNoBackdrop()
    {
        // Act
        IRenderedComponent<ReviewPopupHost> cut = RenderHost();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-popup-backdrop']"));
    }

    [Fact]
    public void ReviewPopupHost_BackdropClick_ClosesPopup()
    {
        // Arrange
        ReviewPopupState popups = Services.GetRequiredService<ReviewPopupState>();
        popups.Toggle(ReviewPopupState.Camera);
        IRenderedComponent<ReviewPopupHost> cut = RenderHost();

        // Act
        cut.Find("[data-testid='review-popup-backdrop']").Click();

        // Assert
        Assert.Null(popups.Open);
    }

    [Fact]
    public void ReviewPopupHost_Escape_ClosesPopup()
    {
        // Arrange
        ReviewPopupState popups = Services.GetRequiredService<ReviewPopupState>();
        popups.Toggle(ReviewPopupState.Filter);
        IRenderedComponent<ReviewPopupHost> cut = RenderHost();

        // Act
        cut.Find("[data-testid='review-popup-host']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        // Assert
        Assert.Null(popups.Open);
    }
}
