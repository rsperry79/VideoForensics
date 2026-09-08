using Xunit;

namespace VideoForensics.Providers.Uniview.Tests
{
    /// <summary>
    /// Tests for HevcNalReassembler - reassembles HEVC NAL units from RTP payloads into Annex-B format.
    /// These are pure byte-manipulation tests with no network dependency.
    /// </summary>
    public class HevcNalReassemblerTests
    {
        private static readonly byte[] RtpHeaderBase =
        [
            0x80, // V=2, P=0, X=0, CC=0
            0x6C, // M=0, PT=108 (HEVC dynamic payload type)
            0x00, 0x01, // Sequence number
            0x00, 0x00, 0x00, 0x00, // Timestamp
            0x00, 0x00, 0x00, 0x01, // SSRC
        ];

        [Fact]
        public void HevcNalReassembler_ProcessSingleNalUnit_WritesProperly()
        {
            // Arrange
            using var output = new MemoryStream();
            var reassembler = new HevcNalReassembler(output);

            // Build a minimal RTP packet with a single NAL unit (VPS, type 0)
            var nal = new byte[] { 0x00, 0xAA, 0xBB, 0xCC }; // NAL header + payload
            var rtpPacket = new byte[RtpHeaderBase.Length + nal.Length];
            Array.Copy(RtpHeaderBase, rtpPacket, RtpHeaderBase.Length);
            Array.Copy(nal, 0, rtpPacket, RtpHeaderBase.Length, nal.Length);

            // Act
            reassembler.ProcessRtpPacket(rtpPacket);

            // Assert
            var result = output.ToArray();
            var startCode = new byte[] { 0x00, 0x00, 0x00, 0x01 };
            Assert.True(result.AsSpan().StartsWith(startCode));
            Assert.True(result.AsSpan().EndsWith(nal));
        }

        [Fact]
        public void HevcNalReassembler_ProcessFragmentedNalUnit_ReassemblesCorrectly()
        {
            // Arrange
            using var output = new MemoryStream();
            var reassembler = new HevcNalReassembler(output);

            // Create a fragmented NAL unit (FU type 49)
            // Start fragment: FU indicator + FU header (Start=1, End=0, Type=0x20)
            var fragmentStart = new byte[]
            {
                0x62, 0x00,  // FU indicator (type 49) + preserved bits
                0x80 | 0x20, // FU header: S=1, E=0, type=0x20
                0xAA, 0xBB,  // Payload fragment 1
            };

            var rtpPacketStart = new byte[RtpHeaderBase.Length + fragmentStart.Length];
            Array.Copy(RtpHeaderBase, rtpPacketStart, RtpHeaderBase.Length);
            Array.Copy(fragmentStart, 0, rtpPacketStart, RtpHeaderBase.Length, fragmentStart.Length);

            // Middle fragment: FU header (Start=0, End=0, Type=0x20)
            var fragmentMiddle = new byte[]
            {
                0x62, 0x00,   // FU indicator
                0x20,         // FU header: S=0, E=0, type=0x20
                0xCC, 0xDD,   // Payload fragment 2
            };

            var rtpPacketMiddle = new byte[RtpHeaderBase.Length + fragmentMiddle.Length];
            Array.Copy(RtpHeaderBase, rtpPacketMiddle, RtpHeaderBase.Length);
            Array.Copy(fragmentMiddle, 0, rtpPacketMiddle, RtpHeaderBase.Length, fragmentMiddle.Length);

            // End fragment: FU header (Start=0, End=1, Type=0x20)
            var fragmentEnd = new byte[]
            {
                0x62, 0x00,  // FU indicator
                0x40 | 0x20, // FU header: S=0, E=1, type=0x20
                0xEE, 0xFF,  // Payload fragment 3
            };

            var rtpPacketEnd = new byte[RtpHeaderBase.Length + fragmentEnd.Length];
            Array.Copy(RtpHeaderBase, rtpPacketEnd, RtpHeaderBase.Length);
            Array.Copy(fragmentEnd, 0, rtpPacketEnd, RtpHeaderBase.Length, fragmentEnd.Length);

            // Act
            reassembler.ProcessRtpPacket(rtpPacketStart);
            reassembler.ProcessRtpPacket(rtpPacketMiddle);
            reassembler.ProcessRtpPacket(rtpPacketEnd);

            // Assert
            var result = output.ToArray();
            var startCode = new byte[] { 0x00, 0x00, 0x00, 0x01 };
            Assert.True(result.AsSpan().StartsWith(startCode));

            // The reassembled NAL should contain: reconstructed header + all fragment payloads
            // Reconstructed byte 0: (0x62 & 0x81) | (0x20 << 1) = 0x62 | 0x40 = 0x62
            // Reconstructed byte 1: 0x00
            // Followed by all payload fragments: 0xAA 0xBB 0xCC 0xDD 0xEE 0xFF
            var expectedPayload = new byte[] { 0x62, 0x00, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
            Assert.Equal(startCode.Length + expectedPayload.Length, result.Length);
        }

        [Fact]
        public void HevcNalReassembler_IgnoresTooShortPacket()
        {
            // Arrange
            using var output = new MemoryStream();
            var reassembler = new HevcNalReassembler(output);

            // RTP packets must be at least 12 bytes
            var tooShort = new byte[] { 0x80, 0x6C };

            // Act
            reassembler.ProcessRtpPacket(tooShort);

            // Assert
            Assert.Empty(output.ToArray());
        }

        [Fact]
        public void HevcNalReassembler_IgnoresInvalidRtpVersion()
        {
            // Arrange
            using var output = new MemoryStream();
            var reassembler = new HevcNalReassembler(output);

            // Set version to 3 (invalid, should be 2)
            var packet = new byte[RtpHeaderBase.Length + 4];
            Array.Copy(RtpHeaderBase, packet, RtpHeaderBase.Length);
            packet[0] = 0xC0; // V=3

            // Act
            reassembler.ProcessRtpPacket(packet);

            // Assert
            Assert.Empty(output.ToArray());
        }

        [Fact]
        public void HevcNalReassembler_HandlesRtpExtensionHeaders()
        {
            // Arrange
            using var output = new MemoryStream();
            var reassembler = new HevcNalReassembler(output);

            // Build packet with RTP extension (X bit set)
            var header = new byte[]
            {
                0x90, // V=2, P=0, X=1, CC=0
                0x6C, // M=0, PT=108
                0x00, 0x01, // Sequence
                0x00, 0x00, 0x00, 0x00, // Timestamp
                0x00, 0x00, 0x00, 0x01, // SSRC
                0x00, 0x01, // Extension profile
                0x00, 0x01, // Extension length (1 word = 4 bytes)
                0xFF, 0xFF, 0xFF, 0xFF, // Extension data
            };

            var nal = new byte[] { 0x00, 0xAA, 0xBB, 0xCC };
            var rtpPacket = new byte[header.Length + nal.Length];
            Array.Copy(header, rtpPacket, header.Length);
            Array.Copy(nal, 0, rtpPacket, header.Length, nal.Length);

            // Act
            reassembler.ProcessRtpPacket(rtpPacket);

            // Assert
            var result = output.ToArray();
            var startCode = new byte[] { 0x00, 0x00, 0x00, 0x01 };
            Assert.True(result.AsSpan().StartsWith(startCode));
            Assert.True(result.AsSpan().EndsWith(nal));
        }

        [Fact]
        public void HevcNalReassembler_MultipleSequentialNals_WritesAllWithStartCodes()
        {
            // Arrange
            using var output = new MemoryStream();
            var reassembler = new HevcNalReassembler(output);

            var nal1 = new byte[] { 0x00, 0xAA, 0xBB };
            var nal2 = new byte[] { 0x00, 0xCC, 0xDD };

            var packet1 = new byte[RtpHeaderBase.Length + nal1.Length];
            Array.Copy(RtpHeaderBase, packet1, RtpHeaderBase.Length);
            Array.Copy(nal1, 0, packet1, RtpHeaderBase.Length, nal1.Length);

            var packet2 = new byte[RtpHeaderBase.Length + nal2.Length];
            Array.Copy(RtpHeaderBase, packet2, RtpHeaderBase.Length);
            Array.Copy(nal2, 0, packet2, RtpHeaderBase.Length, nal2.Length);

            // Act
            reassembler.ProcessRtpPacket(packet1);
            reassembler.ProcessRtpPacket(packet2);

            // Assert
            var result = output.ToArray();
            var startCode = new byte[] { 0x00, 0x00, 0x00, 0x01 };
            // Expected: [startCode][nal1][startCode][nal2]
            var expected = new byte[startCode.Length + nal1.Length + startCode.Length + nal2.Length];
            var pos = 0;
            Array.Copy(startCode, 0, expected, pos, startCode.Length); pos += startCode.Length;
            Array.Copy(nal1, 0, expected, pos, nal1.Length); pos += nal1.Length;
            Array.Copy(startCode, 0, expected, pos, startCode.Length); pos += startCode.Length;
            Array.Copy(nal2, 0, expected, pos, nal2.Length);

            Assert.Equal(expected, result);
        }
    }
}
