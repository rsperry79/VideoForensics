#nullable disable
using VideoForensics.Providers.Ring.Models;

namespace VideoForensics.Providers.Ring.Tests
{
    public class FilterTests
    {
        [Fact]
        public void Filter_DefaultVideoCountIs10000()
        {
            var filter = new Filter();
            Assert.Equal(10000, filter.VideoCount);
        }

        [Fact]
        public void Filter_CanSetVideoCount()
        {
            var filter = new Filter { VideoCount = 100 };
            Assert.Equal(100, filter.VideoCount);
        }

        [Fact]
        public void Filter_CanSetStartDateTime()
        {
            var now = DateTime.Now;
            var filter = new Filter { StartDateTime = now };
            Assert.Equal(now, filter.StartDateTime);
        }

        [Fact]
        public void Filter_CanSetEndDateTime()
        {
            var now = DateTime.Now;
            var filter = new Filter { EndDateTime = now };
            Assert.Equal(now, filter.EndDateTime);
        }

        [Fact]
        public void Filter_StartAndEndDateCanBeDifferent()
        {
            var start = new DateTime(2026, 1, 1);
            var end = new DateTime(2026, 1, 31);

            var filter = new Filter
            {
                StartDateTime = start,
                EndDateTime = end
            };

            Assert.Equal(start, filter.StartDateTime);
            Assert.Equal(end, filter.EndDateTime);
        }

        [Fact]
        public void Filter_CanSetEndDateBeforeStart()
        {
            var start = new DateTime(2026, 1, 31);
            var end = new DateTime(2026, 1, 1);

            var filter = new Filter
            {
                StartDateTime = start,
                EndDateTime = end
            };

            Assert.Equal(start, filter.StartDateTime);
            Assert.Equal(end, filter.EndDateTime);
        }

        [Fact]
        public void Filter_CanModifyPropertiesAfterCreation()
        {
            var filter = new Filter { VideoCount = 50 };
            Assert.Equal(50, filter.VideoCount);

            filter.VideoCount = 200;
            Assert.Equal(200, filter.VideoCount);
        }

        [Fact]
        public void Filter_AllPropertiesCanBeSetTogether()
        {
            var start = DateTime.Now;
            var end = start.AddDays(7);

            var filter = new Filter
            {
                VideoCount = 500,
                StartDateTime = start,
                EndDateTime = end
            };

            Assert.Equal(500, filter.VideoCount);
            Assert.Equal(start, filter.StartDateTime);
            Assert.Equal(end, filter.EndDateTime);
        }

        [Fact]
        public void Filter_DateTimeCanBeSet()
        {
            var filter = new Filter();
            var now = DateTime.Now;

            filter.StartDateTime = now;
            filter.EndDateTime = now.AddDays(1);

            Assert.NotNull(filter.StartDateTime);
            Assert.NotNull(filter.EndDateTime);
        }

        [Fact]
        public void Filter_LargeVideoCountHandling()
        {
            var filter = new Filter { VideoCount = 1000000 };
            Assert.Equal(1000000, filter.VideoCount);
        }

        [Fact]
        public void Filter_ZeroVideoCount()
        {
            var filter = new Filter { VideoCount = 0 };
            Assert.Equal(0, filter.VideoCount);
        }

        [Fact]
        public void Filter_SameDateForStartAndEnd()
        {
            var date = new DateTime(2026, 6, 15);
            var filter = new Filter
            {
                StartDateTime = date,
                EndDateTime = date
            };

            Assert.Equal(date, filter.StartDateTime);
            Assert.Equal(date, filter.EndDateTime);
            Assert.Equal(filter.StartDateTime, filter.EndDateTime);
        }
    }
}
