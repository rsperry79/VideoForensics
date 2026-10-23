using System.Text;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Reads and writes the JPEG COM (comment) marker, used to embed an event ID in snapshot JPEGs.
    /// Why: ffmpeg writes no comment into JPEG output, so the tag is written directly into the file's segments.
    /// </summary>
    internal static class JpegCommentSegment
    {
        private const byte Prefix = 0xFF;
        private const byte StartOfImage = 0xD8;
        private const byte EndOfImage = 0xD9;
        private const byte StartOfScan = 0xDA;
        private const byte Comment = 0xFE;

        // The segment length is 16-bit and includes its own two bytes.
        private const int MaxPayloadBytes = 65533;

        /// <summary>Returns a copy of the JPEG with a COM segment carrying <paramref name="comment"/> inserted after SOI.</summary>
        /// <exception cref="ArgumentException">The bytes are not a JPEG, or the comment does not fit in one segment.</exception>
        public static byte[] Insert(byte[] jpeg, string comment)
        {
            if (!IsStartOfImage(jpeg))
            {
                throw new ArgumentException("Input is not a JPEG (missing start-of-image marker).", nameof(jpeg));
            }

            byte[] payload = Encoding.UTF8.GetBytes(comment);
            if (payload.Length > MaxPayloadBytes)
            {
                throw new ArgumentException($"Comment is {payload.Length} bytes; a JPEG COM segment holds at most {MaxPayloadBytes}.", nameof(comment));
            }

            int segmentLength = payload.Length + 2;
            var result = new byte[jpeg.Length + 4 + payload.Length];
            result[0] = Prefix;
            result[1] = StartOfImage;
            result[2] = Prefix;
            result[3] = Comment;
            result[4] = (byte)(segmentLength >> 8);
            result[5] = (byte)segmentLength;
            payload.CopyTo(result, 6);
            Array.Copy(jpeg, 2, result, 6 + payload.Length, jpeg.Length - 2);
            return result;
        }

        /// <summary>Returns the first COM segment's text, or null if the bytes are not a JPEG or have no COM segment before the scan data.</summary>
        public static string? TryRead(byte[] jpeg)
        {
            if (!IsStartOfImage(jpeg))
            {
                return null;
            }

            int i = 2;
            while (i + 1 < jpeg.Length)
            {
                if (jpeg[i] != Prefix)
                {
                    return null;
                }

                byte marker = jpeg[i + 1];
                if (marker == Prefix)
                {
                    // Fill byte before a marker; skip one byte and look again.
                    i++;
                    continue;
                }

                // Stop at end-of-image or scan data: no COM segment comes after entropy-coded data.
                if (marker == EndOfImage || marker == StartOfScan)
                {
                    return null;
                }

                // Standalone markers (TEM, RSTn) carry no length field.
                if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    i += 2;
                    continue;
                }

                if (i + 4 > jpeg.Length)
                {
                    return null;
                }

                int segmentLength = (jpeg[i + 2] << 8) | jpeg[i + 3];
                if (segmentLength < 2 || i + 2 + segmentLength > jpeg.Length)
                {
                    return null;
                }

                if (marker == Comment)
                {
                    return Encoding.UTF8.GetString(jpeg, i + 4, segmentLength - 2);
                }

                i += 2 + segmentLength;
            }

            return null;
        }

        private static bool IsStartOfImage(byte[] bytes) =>
            bytes.Length >= 2 && bytes[0] == Prefix && bytes[1] == StartOfImage;
    }
}
