using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>How RingMediaDownloadService maps the real HTTP status and body of a latest-snapshot read to a capture attempt. Session fakes are not used here: Session is concrete.</summary>
    public class RingSnapshotCaptureClassificationTests
    {
        [Fact]
        public void ClassifySnapshotResponse_200WithJpegBody_Returns200WithTheImage()
        {
            byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

            var attempt = RingMediaDownloadService.ClassifySnapshotResponse(200, jpeg);

            Assert.Equal(200, attempt.HttpStatus);
            Assert.Same(jpeg, attempt.Image);
            Assert.Null(attempt.ThrottlePause);
        }

        [Fact]
        public void ClassifySnapshotResponse_200WithNonJpegBody_Returns200WithNoImage()
        {
            // The status is what Ring returned. A 200 with a non-image body is still a 200; the caller treats it as a failed capture.
            byte[] json = "{\"error\":\"not found\"}"u8.ToArray();

            var attempt = RingMediaDownloadService.ClassifySnapshotResponse(200, json);

            Assert.Equal(200, attempt.HttpStatus);
            Assert.Null(attempt.Image);
        }

        [Fact]
        public void ClassifySnapshotResponse_404WithJsonBody_Returns404WithNoImage()
        {
            byte[] json = "{\"error\":\"not found\"}"u8.ToArray();

            var attempt = RingMediaDownloadService.ClassifySnapshotResponse(404, json);

            Assert.Equal(404, attempt.HttpStatus);
            Assert.Null(attempt.Image);
        }

        [Fact]
        public void ClassifySnapshotResponse_502WithJpegBody_Returns502WithNoImage()
        {
            // A non-200 status never carries an image, even when the bytes look like a JPEG.
            byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

            var attempt = RingMediaDownloadService.ClassifySnapshotResponse(502, jpeg);

            Assert.Equal(502, attempt.HttpStatus);
            Assert.Null(attempt.Image);
        }

        [Fact]
        public void ClassifySnapshotResponse_200WithEmptyBody_Returns200WithNoImage()
        {
            var attempt = RingMediaDownloadService.ClassifySnapshotResponse(200, []);

            Assert.Equal(200, attempt.HttpStatus);
            Assert.Null(attempt.Image);
        }

        [Fact]
        public void HasJpegSignature_TwoBytesOnly_ReturnsFalse()
        {
            Assert.False(RingMediaDownloadService.HasJpegSignature([0xFF, 0xD8]));
        }
    }
}
