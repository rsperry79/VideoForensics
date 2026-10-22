using VideoForensics.Core.Providers.Core;
using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class TimelineSizeTests
    {
        [Fact]
        public void Cap_TallerThanLimit_ScalesToLimitKeepingAspect()
        {
            // 1920x1080 at 360 high is 640x360 exactly.
            Assert.Equal((640, 360), TimelineSize.Cap(1920, 1080, 360));
        }

        [Fact]
        public void Cap_OddScaledWidth_RoundsUpToEven()
        {
            // 1280x720 at 360 high is 640x360; 1000x750 at 360 high is 480x360.
            Assert.Equal((480, 360), TimelineSize.Cap(1000, 750, 360));
            // 999x1000 at 360 high gives 359.6 wide, which rounds to 360 (even).
            Assert.Equal((360, 360), TimelineSize.Cap(999, 1000, 360));
        }

        [Fact]
        public void Cap_AlreadyWithinLimit_ReturnsSourceSize()
        {
            Assert.Equal((640, 360), TimelineSize.Cap(640, 360, 360));
            Assert.Equal((320, 240), TimelineSize.Cap(320, 240, 360));
        }
    }
}
