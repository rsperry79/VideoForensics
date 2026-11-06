using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    /// <summary>Parses one "pts_time,size" line from ffprobe's packet listing; lines without a usable time or size are skipped.</summary>
    public class FfprobePacketReaderTests
    {
        [Fact]
        public void FfprobePacketReader_ParsePacketLine_ReadsTimeAndSize()
        {
            (double PtsSeconds, long SizeBytes)? packet = FfprobePacketReader.ParsePacketLine("1.500000,2048");

            Assert.Equal((1.5, 2048L), packet);
        }

        [Fact]
        public void FfprobePacketReader_ParsePacketLine_MissingTime_ReturnsNull()
        {
            Assert.Null(FfprobePacketReader.ParsePacketLine("N/A,2048"));
        }

        [Fact]
        public void FfprobePacketReader_ParsePacketLine_MissingSize_ReturnsNull()
        {
            Assert.Null(FfprobePacketReader.ParsePacketLine("0.000000,"));
        }

        [Fact]
        public void FfprobePacketReader_ParsePacketLine_Garbage_ReturnsNull()
        {
            Assert.Null(FfprobePacketReader.ParsePacketLine("not a packet"));
        }
    }
}
