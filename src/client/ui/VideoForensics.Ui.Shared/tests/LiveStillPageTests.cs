using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests
{
    public class LiveStillPageTests : BunitContext
    {
        [Fact]
        public void LiveStill_ShowsTheStoredStillImageFromTheApi()
        {
            // Arrange
            var stillId = Guid.NewGuid();
            Services.AddLocalization();

            // Act
            IRenderedComponent<VideoForensics.Client.Ui.Shared.Pages.LiveStill> cut =
                Render<VideoForensics.Client.Ui.Shared.Pages.LiveStill>(p => p.Add(c => c.StillId, stillId));

            // Assert: the image comes from the still endpoint, so the browser sends the signed-in session with it
            Assert.Equal($"/api/v1/media/{stillId}/still", cut.Find("[data-testid='live-still-image']").GetAttribute("src"));
        }
    }
}
