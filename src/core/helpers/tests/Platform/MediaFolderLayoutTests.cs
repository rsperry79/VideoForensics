using System;
using System.IO;

using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Core.Helpers.Tests.Platform
{
    public class MediaFolderLayoutTests
    {
        [Fact]
        public void GetDeviceFolder_BuildsProviderLocationDevice_LowercasedWithUnderscores()
        {
            string result = MediaFolderLayout.GetDeviceFolder(@"D:\media", "Ring", "Arlington", "Front Door");

            Assert.Equal(Path.Combine(@"D:\media", "ring", "arlington", "front_door"), result);
        }

        [Fact]
        public void GetDeviceFolder_RemovesInvalidCharacters()
        {
            string result = MediaFolderLayout.GetDeviceFolder(@"D:\media", "Ring", "Main/St", "Cam:1");

            Assert.Equal(Path.Combine(@"D:\media", "ring", "mainst", "cam1"), result);
        }

        [Fact]
        public void GetDeviceFolder_BlankSegmentBecomesUnknown()
        {
            string result = MediaFolderLayout.GetDeviceFolder(@"D:\media", "Ring", "  ", "");

            Assert.Equal(Path.Combine(@"D:\media", "ring", "unknown", "unknown"), result);
        }
    }
}
