using Xunit;

namespace VideoForensics.Providers.Uniview.Tests
{
    /// <summary>
    /// Tests for RtpAudioExtractor - extracts G.711 mu-law audio from RTP payloads.
    /// These are pure byte-manipulation tests with no network dependency.
    /// </summary>
    public class RtpAudioExtractorTests
    {
        private static readonly byte[] RtpHeaderBase =
        [
            0x80, // V=2, P=0, X=0, CC=0
            0x00, // M=0, PT=0 (PCMU/G.711 mu-law)
            0x00, 0x01, // Sequence number
            0x00, 0x00, 0x00, 0x00, // Timestamp
            0x00, 0x00, 0x00, 0x01, // SSRC
        ];

        [Fact]
        public void RtpAudioExtractor_ProcessGpayload_ExtractsAudioSamples()
        {
            // Arrange
            using var output = new MemoryStream();
            var extractor = new RtpAudioExtractor(output);

            // G.711 payload: 8 raw mu-law samples
            var audioPayload = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xFB, 0xFA, 0xF9, 0xF8 };
            var rtpPacket = new byte[RtpHeaderBase.Length + audioPayload.Length];
            Array.Copy(RtpHeaderBase, rtpPacket, RtpHeaderBase.Length);
            Array.Copy(audioPayload, 0, rtpPacket, RtpHeaderBase.Length, audioPayload.Length);

            // Act
            extractor.ProcessRtpPacket(rtpPacket);

            // Assert
            Assert.Equal(audioPayload, output.ToArray());
        }

        [Fact]
        public void RtpAudioExtractor_IgnoresTooShortPacket()
        {
            // Arrange
            using var output = new MemoryStream();
            var extractor = new RtpAudioExtractor(output);

            // RTP packets must be at least 12 bytes
            var tooShort = new byte[] { 0x80, 0x00 };

            // Act
            extractor.ProcessRtpPacket(tooShort);

            // Assert
            Assert.Empty(output.ToArray());
        }

        [Fact]
        public void RtpAudioExtractor_IgnoresInvalidRtpVersion()
        {
            // Arrange
            using var output = new MemoryStream();
            var extractor = new RtpAudioExtractor(output);

            // Set version to 1 (invalid, should be 2)
            var packet = new byte[RtpHeaderBase.Length + 8];
            Array.Copy(RtpHeaderBase, packet, RtpHeaderBase.Length);
            packet[0] = 0x40; // V=1

            // Act
            extractor.ProcessRtpPacket(packet);

            // Assert
            Assert.Empty(output.ToArray());
        }

        [Fact]
        public void RtpAudioExtractor_MultiplePackets_ConcatenatesAllPayloads()
        {
            // Arrange
            using var output = new MemoryStream();
            var extractor = new RtpAudioExtractor(output);

            var payload1 = new byte[] { 0x11, 0x22, 0x33, 0x44 };
            var payload2 = new byte[] { 0x55, 0x66, 0x77, 0x88 };

            var packet1 = new byte[RtpHeaderBase.Length + payload1.Length];
            Array.Copy(RtpHeaderBase, packet1, RtpHeaderBase.Length);
            Array.Copy(payload1, 0, packet1, RtpHeaderBase.Length, payload1.Length);

            var packet2 = new byte[RtpHeaderBase.Length + payload2.Length];
            Array.Copy(RtpHeaderBase, packet2, RtpHeaderBase.Length);
            Array.Copy(payload2, 0, packet2, RtpHeaderBase.Length, payload2.Length);

            // Act
            extractor.ProcessRtpPacket(packet1);
            extractor.ProcessRtpPacket(packet2);

            // Assert
            var expected = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };
            Assert.Equal(expected, output.ToArray());
        }

        [Fact]
        public void RtpAudioExtractor_HandlesRtpExtensionHeaders()
        {
            // Arrange
            using var output = new MemoryStream();
            var extractor = new RtpAudioExtractor(output);

            // Build packet with RTP extension (X bit set)
            var header = new byte[]
            {
                0x90, // V=2, P=0, X=1, CC=0
                0x00, // M=0, PT=0 (PCMU)
                0x00, 0x01, // Sequence
                0x00, 0x00, 0x00, 0x00, // Timestamp
                0x00, 0x00, 0x00, 0x01, // SSRC
                0x00, 0x01, // Extension profile
                0x00, 0x01, // Extension length (1 word = 4 bytes)
                0xFF, 0xFF, 0xFF, 0xFF, // Extension data
            };

            var payload = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            var rtpPacket = new byte[header.Length + payload.Length];
            Array.Copy(header, rtpPacket, header.Length);
            Array.Copy(payload, 0, rtpPacket, header.Length, payload.Length);

            // Act
            extractor.ProcessRtpPacket(rtpPacket);

            // Assert
            Assert.Equal(payload, output.ToArray());
        }

        [Fact]
        public void RtpAudioExtractor_HandlesRtpContributingSource()
        {
            // Arrange
            using var output = new MemoryStream();
            var extractor = new RtpAudioExtractor(output);

            // Build packet with CSRC list (CC=2)
            var header = new byte[]
            {
                0x82, // V=2, P=0, X=0, CC=2
                0x00, // M=0, PT=0
                0x00, 0x01, // Sequence
                0x00, 0x00, 0x00, 0x00, // Timestamp
                0x00, 0x00, 0x00, 0x01, // SSRC
                0x11, 0x11, 0x11, 0x11, // CSRC 1
                0x22, 0x22, 0x22, 0x22, // CSRC 2
            };

            var payload = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            var rtpPacket = new byte[header.Length + payload.Length];
            Array.Copy(header, rtpPacket, header.Length);
            Array.Copy(payload, 0, rtpPacket, header.Length, payload.Length);

            // Act
            extractor.ProcessRtpPacket(rtpPacket);

            // Assert
            Assert.Equal(payload, output.ToArray());
        }

        [Fact]
        public void RtpAudioExtractor_EmptyPayload_WritesNothing()
        {
            // Arrange
            using var output = new MemoryStream();
            var extractor = new RtpAudioExtractor(output);

            // RTP packet with no payload
            var rtpPacket = new byte[RtpHeaderBase.Length];
            Array.Copy(RtpHeaderBase, rtpPacket, RtpHeaderBase.Length);

            // Act
            extractor.ProcessRtpPacket(rtpPacket);

            // Assert
            Assert.Empty(output.ToArray());
        }
    }
}
