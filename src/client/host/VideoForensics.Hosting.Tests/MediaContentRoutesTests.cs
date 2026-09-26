using VideoForensics.Api.Contracts;

using Xunit;

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
