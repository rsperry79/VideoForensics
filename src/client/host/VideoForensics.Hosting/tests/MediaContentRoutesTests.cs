using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Hosting.Tests
{
    public class MediaContentRoutesTests
    {
        [Fact]
        public void ContentUrl_BuildsCorrectUrl()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            string ticketToken = "test-token-abc123";

            // Act
            string url = MediaContentRoutes.ContentUrl(mediaItemId, ticketToken);

            // Assert
            string expected = $"/api/v1/media/{mediaItemId}/content?ticket=test-token-abc123";
            Assert.Equal(expected, url);
        }

        [Fact]
        public void ThumbnailUrlFromContentUrl_SwapsRouteAndKeepsTheTicket()
        {
            var mediaItemId = Guid.NewGuid();
            string contentUrl = MediaContentRoutes.ContentUrl(mediaItemId, "tok");

            string thumbnail = MediaContentRoutes.ThumbnailUrlFromContentUrl(contentUrl);

            Assert.Equal($"/api/v1/media/{mediaItemId}/thumbnail?ticket=tok", thumbnail);
        }

        [Fact]
        public void ThumbnailUrlFromContentUrl_NotAContentUrl_ReturnsInputUnchanged()
        {
            Assert.Equal("https://cdn.test/a.jpg", MediaContentRoutes.ThumbnailUrlFromContentUrl("https://cdn.test/a.jpg"));
        }

        [Fact]
        public void ContentUrl_EscapesSpecialCharactersInToken()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            string ticketToken = "token+with/special?chars&more=stuff";

            // Act
            string url = MediaContentRoutes.ContentUrl(mediaItemId, ticketToken);

            // Assert
            Assert.Contains($"/api/v1/media/{mediaItemId}/content?ticket=", url);
            // The token should be escaped in the query string
            Assert.Contains("token%2Bwith%2Fspecial%3Fchars%26more%3Dstuff", url);
        }
    }
}
