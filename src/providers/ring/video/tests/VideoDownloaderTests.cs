#nullable disable
using System.Net;
using System.Net.Http;
using System.Text;

using VideoForensics.Providers.Ring;

using Moq;

namespace VideoForensics.Providers.Ring.Video.Tests
{
    [TestClass]
    public class VideoDownloaderTests
    {
        private static VideoDownloader CreateDownloader(FakeHttpMessageHandler handler)
        {
            return new VideoDownloader(new HttpClient(handler));
        }

        [TestMethod]
        public async Task OpenStreamAsync_ReturnsDownloadedBytes()
        {
            var expected = Encoding.UTF8.GetBytes("fake-video-bytes");
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expected)
            });
            var downloader = CreateDownloader(handler);

            using var stream = await downloader.OpenStreamAsync("https://example.com/recording.mp4");
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            CollectionAssert.AreEqual(expected, ms.ToArray());
        }

        [TestMethod]
        public async Task OpenStreamAsync_RequestsWithRangeHeader()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[0])
            });
            var downloader = CreateDownloader(handler);

            await downloader.OpenStreamAsync("https://example.com/recording.mp4");

            Assert.IsNotNull(handler.LastRequest.Headers.Range);
            Assert.AreEqual("https://example.com/recording.mp4", handler.LastRequest.RequestUri.ToString());
        }

        [TestMethod]
        public async Task DownloadToFileAsync_WritesBytesToDisk()
        {
            var expected = Encoding.UTF8.GetBytes("fake-video-bytes-for-file");
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expected)
            });
            var downloader = CreateDownloader(handler);
            var path = Path.Combine(Path.GetTempPath(), $"video-downloader-test-{Guid.NewGuid()}.bin");

            try
            {
                await downloader.DownloadToFileAsync("https://example.com/recording.mp4", path);

                CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(path));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public async Task OpenStreamAsync_InvalidUrl_ThrowsArgumentException()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var downloader = CreateDownloader(handler);

            try
            {
                await downloader.OpenStreamAsync("not-a-url");
                Assert.Fail("Expected an ArgumentException to be thrown");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }

        [TestMethod]
        public async Task OpenStreamAsync_EmptyUrl_ThrowsArgumentException()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var downloader = CreateDownloader(handler);

            try
            {
                await downloader.OpenStreamAsync("");
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
        [TestMethod]
        public async Task IVideoDownloader_IsMockable()
        {
            var mock = new Mock<IVideoDownloader>();
            mock.Setup(d => d.OpenStreamAsync(It.IsAny<string>()))
                .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("mocked")));

            IVideoDownloader downloader = mock.Object;
            using var stream = await downloader.OpenStreamAsync("irrelevant-url");
            using var reader = new StreamReader(stream);

            Assert.AreEqual("mocked", await reader.ReadToEndAsync());
            mock.Verify(d => d.OpenStreamAsync("irrelevant-url"), Times.Once);
        }
    }
}
