namespace VideoForensics.Client.Hosting.Tests.Services
{
    public class SyncActivityLogTests
    {
        [Fact]
        public void Drain_ReturnsLinesOldestFirst_AndEmptiesTheLog()
        {
            var log = new SyncActivityLog();
            log.Append("first");
            log.Append("second");

            Assert.Equal(new[] { "first", "second" }, log.Drain());
            Assert.Empty(log.Drain());
        }

        [Fact]
        public void Append_OverCapacity_DropsOldestLines()
        {
            var log = new SyncActivityLog();
            for (int i = 0; i < SyncActivityLog.MaxPendingLines + 5; i++)
            {
                log.Append($"line {i}");
            }

            IReadOnlyList<string> lines = log.Drain();

            Assert.Equal(SyncActivityLog.MaxPendingLines, lines.Count);
            Assert.Equal("line 5", lines[0]);
        }
    }
}
