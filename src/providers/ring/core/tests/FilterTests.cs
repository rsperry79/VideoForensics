#nullable disable
using VideoForensics.Providers.Ring.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VideoForensics.Providers.Ring.Tests
{
    [TestClass]
    public class FilterTests
    {
        [TestMethod]
        public void Filter_DefaultVideoCountIs10000()
        {
            var filter = new Filter();
            Assert.AreEqual(10000, filter.VideoCount);
        }

        [TestMethod]
        public void Filter_CanSetVideoCount()
        {
            var filter = new Filter { VideoCount = 100 };
            Assert.AreEqual(100, filter.VideoCount);
        }

        [TestMethod]
        public void Filter_CanSetStartDateTime()
        {
            var now = DateTime.Now;
            var filter = new Filter { StartDateTime = now };
            Assert.AreEqual(now, filter.StartDateTime);
        }

        [TestMethod]
        public void Filter_CanSetEndDateTime()
        {
            var now = DateTime.Now;
            var filter = new Filter { EndDateTime = now };
            Assert.AreEqual(now, filter.EndDateTime);
        }

        [TestMethod]
        public void Filter_StartAndEndDateCanBeDifferent()
        {
            var start = new DateTime(2026, 1, 1);
            var end = new DateTime(2026, 1, 31);

            var filter = new Filter
            {
                StartDateTime = start,
                EndDateTime = end
            };

            Assert.AreEqual(start, filter.StartDateTime);
            Assert.AreEqual(end, filter.EndDateTime);
        }

        [TestMethod]
        public void Filter_CanSetEndDateBeforeStart()
        {
            var start = new DateTime(2026, 1, 31);
            var end = new DateTime(2026, 1, 1);

            var filter = new Filter
            {
                StartDateTime = start,
                EndDateTime = end
            };

            Assert.AreEqual(start, filter.StartDateTime);
            Assert.AreEqual(end, filter.EndDateTime);
        }

        [TestMethod]
        public void Filter_CanModifyPropertiesAfterCreation()
        {
            var filter = new Filter { VideoCount = 50 };
            Assert.AreEqual(50, filter.VideoCount);

            filter.VideoCount = 200;
            Assert.AreEqual(200, filter.VideoCount);
        }

        [TestMethod]
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

            Assert.AreEqual(500, filter.VideoCount);
            Assert.AreEqual(start, filter.StartDateTime);
            Assert.AreEqual(end, filter.EndDateTime);
        }

        [TestMethod]
        public void Filter_DateTimeCanBeSet()
        {
            var filter = new Filter();
            var now = DateTime.Now;

            filter.StartDateTime = now;
            filter.EndDateTime = now.AddDays(1);

            Assert.IsNotNull(filter.StartDateTime);
            Assert.IsNotNull(filter.EndDateTime);
        }

        [TestMethod]
        public void Filter_LargeVideoCountHandling()
        {
            var filter = new Filter { VideoCount = 1000000 };
            Assert.AreEqual(1000000, filter.VideoCount);
        }

        [TestMethod]
        public void Filter_ZeroVideoCount()
        {
            var filter = new Filter { VideoCount = 0 };
            Assert.AreEqual(0, filter.VideoCount);
        }

        [TestMethod]
        public void Filter_SameDateForStartAndEnd()
        {
            var date = new DateTime(2026, 6, 15);
            var filter = new Filter
            {
                StartDateTime = date,
                EndDateTime = date
            };

            Assert.AreEqual(date, filter.StartDateTime);
            Assert.AreEqual(date, filter.EndDateTime);
            Assert.AreEqual(filter.StartDateTime, filter.EndDateTime);
        }
    }
}
