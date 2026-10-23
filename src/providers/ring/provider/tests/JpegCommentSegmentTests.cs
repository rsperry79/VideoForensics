using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    public class JpegCommentSegmentTests
    {
        // Smallest well-formed JPEG: start-of-image then end-of-image, no scan data.
        private static readonly byte[] MinimalJpeg = { 0xFF, 0xD8, 0xFF, 0xD9 };

        [Fact]
        public void Insert_ThenTryRead_RoundTripsComment()
        {
            byte[] tagged = JpegCommentSegment.Insert(MinimalJpeg, "vf-event-id:abc");

            Assert.Equal("vf-event-id:abc", JpegCommentSegment.TryRead(tagged));
        }

        [Fact]
        public void Insert_KeepsStartOfImageFirstAndOriginalBytesIntact()
        {
            byte[] tagged = JpegCommentSegment.Insert(MinimalJpeg, "vf-event-id:abc");

            // Still a JPEG: SOI first, and the original segments follow the new COM segment unchanged.
            Assert.Equal(0xFF, tagged[0]);
            Assert.Equal(0xD8, tagged[1]);
            Assert.Equal(MinimalJpeg[2..], tagged[^2..]);
        }

        [Fact]
        public void TryRead_JpegWithoutCommentSegment_ReturnsNull()
        {
            Assert.Null(JpegCommentSegment.TryRead(MinimalJpeg));
        }

        [Fact]
        public void TryRead_NonJpegBytes_ReturnsNull()
        {
            Assert.Null(JpegCommentSegment.TryRead(new byte[] { 0x00, 0x01, 0x02 }));
        }

        [Fact]
        public void Insert_NonJpegBytes_Throws()
        {
            _ = Assert.Throws<ArgumentException>(() => JpegCommentSegment.Insert(new byte[] { 0x00, 0x01 }, "vf-event-id:abc"));
        }

        [Fact]
        public void Insert_CommentTooLongForSegment_Throws()
        {
            // A JPEG segment length is 16-bit and includes its own two bytes, so the payload is capped at 65533.
            string tooLong = new('x', 65534);

            _ = Assert.Throws<ArgumentException>(() => JpegCommentSegment.Insert(MinimalJpeg, tooLong));
        }

        [Fact]
        public void TryRead_FindsCommentAfterOtherSegments()
        {
            // APP0 (JFIF-style) segment with a 2-byte payload, then the COM segment we insert, then EOI.
            byte[] withApp0 = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xD9 };

            byte[] tagged = JpegCommentSegment.Insert(withApp0, "vf-event-id:xyz");

            Assert.Equal("vf-event-id:xyz", JpegCommentSegment.TryRead(tagged));
        }
    }
}
