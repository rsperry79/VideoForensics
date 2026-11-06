using VideoForensics.Providers.Ring.Catalog;

using Xunit;

namespace VideoForensics.Providers.Ring.Common.Tests.Catalog
{
    /// <summary>Picks the largest resolution by pixel count, never by text: "640x360" sorts above "1920x1080" as text.</summary>
    public class ResolutionPickerTests
    {
        [Fact]
        public void LargestByPixels_PicksHighestPixelCount_NotLargestText()
        {
            Assert.Equal("1920x1080", ResolutionPicker.LargestByPixels(["640x360", "1920x1080", "1280x720"]));
        }

        [Fact]
        public void LargestByPixels_IgnoresNullAndUnparseableValues()
        {
            Assert.Equal("1280x720", ResolutionPicker.LargestByPixels([null, "", "HD", "1280x720"]));
        }

        [Fact]
        public void LargestByPixels_NoUsableValues_ReturnsNull()
        {
            Assert.Null(ResolutionPicker.LargestByPixels([null, "unknown"]));
        }
    }
}
