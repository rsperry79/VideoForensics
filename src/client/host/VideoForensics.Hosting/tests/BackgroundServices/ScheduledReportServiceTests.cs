using VideoForensics.Client.Hosting.BackgroundServices;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    public class ScheduledReportServiceTests
    {
        [Fact]
        public void WindowEndingAt_IsTheLast24Hours()
        {
            var now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

            (DateTime fromUtc, DateTime toUtc) = ScheduledReportService.WindowEndingAt(now);

            Assert.Equal(now, toUtc);
            Assert.Equal(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc), fromUtc);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-5, 1)]
        [InlineData(6, 6)]
        [InlineData(500, 168)]
        public void IntervalFor_ClampsToOneHourToOneWeek(int configured, int expectedHours)
        {
            Assert.Equal(TimeSpan.FromHours(expectedHours), ScheduledReportService.IntervalFor(configured));
        }
    }
}
