using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Models;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    public class SnapshotFrameExtractorCancellationTests
    {
        /// <summary>Never answers until the request is cancelled, like a stalled Ring CDN.</summary>
        private sealed class StalledHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        [Fact]
        public async Task DownloadAndTagSnapshotAsync_CancelledDuringDownload_ThrowsOperationCanceled()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), "vf-snap-" + Guid.NewGuid().ToString("N"));
            try
            {
                var extractor = new SnapshotFrameExtractor(httpClient: new HttpClient(new StalledHandler()));
                var metadata = new SnapshotMetadata { EventDateTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

                // Before the fix this blocked on .Result and had no token parameter at all.
                _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    extractor.DownloadAndTagSnapshotAsync("https://example.invalid/snap.jpg", metadata, outputDir, cts.Token));
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    Directory.Delete(outputDir, recursive: true);
                }
            }
        }
    }
}
