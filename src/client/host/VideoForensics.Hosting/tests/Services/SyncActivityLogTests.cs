namespace VideoForensics.Client.Hosting.Tests.Services
{
    public class SyncActivityLogTests
    {
        [Fact]
        public void Drain_ReturnsLinesOldestFirst_AndEmptiesTheLog()
        {
            var log = new SyncActivityLog(Microsoft.Extensions.Logging.Abstractions.NullLogger<SyncActivityLog>.Instance);
            log.Append("first");
            log.Append("second");

            Assert.Equal(new[] { "first", "second" }, log.Drain());
            Assert.Empty(log.Drain());
        }

        [Fact]
        public void Append_OverCapacity_DropsOldestLines()
        {
            var log = new SyncActivityLog(Microsoft.Extensions.Logging.Abstractions.NullLogger<SyncActivityLog>.Instance);
            for (int i = 0; i < SyncActivityLog.MaxPendingLines + 5; i++)
            {
                log.Append($"line {i}");
            }

            IReadOnlyList<string> lines = log.Drain();

            Assert.Equal(SyncActivityLog.MaxPendingLines, lines.Count);
            Assert.Equal("line 5", lines[0]);
        }

        [Fact]
        public void ReadSince_DoesNotRemoveLines_SoEveryReaderSeesThem()
        {
            var log = new SyncActivityLog(Microsoft.Extensions.Logging.Abstractions.NullLogger<SyncActivityLog>.Instance);
            log.Append("first");
            log.Append("second");

            IReadOnlyList<string> first = log.ReadSince(0, out long next);
            IReadOnlyList<string> again = log.ReadSince(0, out _);

            Assert.Equal(new[] { "first", "second" }, first);
            Assert.Equal(new[] { "first", "second" }, again);
            Assert.Equal(2, next);
            Assert.Equal(new[] { "first", "second" }, log.Drain());
        }

        [Fact]
        public void ReadSince_FromAPosition_ReturnsOnlyNewerLines()
        {
            var log = new SyncActivityLog(Microsoft.Extensions.Logging.Abstractions.NullLogger<SyncActivityLog>.Instance);
            log.Append("first");
            _ = log.ReadSince(0, out long position);
            log.Append("second");

            Assert.Equal(new[] { "second" }, log.ReadSince(position, out _));
        }
    }
}
