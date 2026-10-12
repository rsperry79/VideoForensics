#nullable disable
using Moq;

using System.Net;
using System.Text;

namespace VideoForensics.Providers.Ring.Video.Tests
{
    public class VideoDownloaderTests
    {
        private static VideoDownloader CreateDownloader(FakeHttpMessageHandler handler)
        {
            return new VideoDownloader(new HttpClient(handler));
        }

        [Fact]
        public async Task OpenStreamAsync_ReturnsDownloadedBytes()
        {
            byte[] expected = Encoding.UTF8.GetBytes("fake-video-bytes");
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expected)
            });
            VideoDownloader downloader = CreateDownloader(handler);

            using Stream stream = await downloader.OpenStreamAsync("https://example.com/recording.mp4");
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            Assert.Equal(expected, ms.ToArray());
        }

        [Fact]
        public async Task OpenStreamAsync_RequestsWithRangeHeader()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[0])
            });
            VideoDownloader downloader = CreateDownloader(handler);

            _ = await downloader.OpenStreamAsync("https://example.com/recording.mp4");

            Assert.NotNull(handler.LastRequest.Headers.Range);
            Assert.Equal("https://example.com/recording.mp4", handler.LastRequest.RequestUri.ToString());
        }

        [Fact]
        public async Task DownloadToFileAsync_WritesBytesToDisk()
        {
            byte[] expected = Encoding.UTF8.GetBytes("fake-video-bytes-for-file");
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expected)
            });
            VideoDownloader downloader = CreateDownloader(handler);
            string path = Path.Combine(Path.GetTempPath(), $"video-downloader-test-{Guid.NewGuid()}.bin");

            try
            {
                await downloader.DownloadToFileAsync("https://example.com/recording.mp4", path);

                Assert.Equal(expected, await File.ReadAllBytesAsync(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public async Task OpenStreamAsync_InvalidUrl_ThrowsArgumentException()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            VideoDownloader downloader = CreateDownloader(handler);

            try
            {
                _ = await downloader.OpenStreamAsync("not-a-url");
                Assert.Fail("Expected an ArgumentException to be thrown");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }

        [Fact]
        public async Task OpenStreamAsync_EmptyUrl_ThrowsArgumentException()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            VideoDownloader downloader = CreateDownloader(handler);

            try
            {
                _ = await downloader.OpenStreamAsync("");
                Assert.Fail("Expected an ArgumentException to be thrown");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }

        /// <summary>
        /// IVideoDownloader exists specifically so consumers like Session can be constructed with a
        /// fake in tests instead of making real HTTP calls - confirms a mock can stand in for it.
        /// </summary>
        [Fact]
        public async Task IVideoDownloader_IsMockable()
        {
            var mock = new Mock<IVideoDownloader>();
            _ = mock.Setup(d => d.OpenStreamAsync(It.IsAny<string>()))
                .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("mocked")));

            IVideoDownloader downloader = mock.Object;
            using Stream stream = await downloader.OpenStreamAsync("irrelevant-url");
            using var reader = new StreamReader(stream);

            Assert.Equal("mocked", await reader.ReadToEndAsync());
            mock.Verify(d => d.OpenStreamAsync("irrelevant-url"), Times.Once);
        }
    }
}
