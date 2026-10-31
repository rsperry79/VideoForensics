using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Client.Hosting.Tests
{
    public class DownloadStatusDtoMappingTests
    {
        [Fact]
        public void DownloadStatusDtoMapping_RoundTrip_PreservesAllFields()
        {
            var original = new DownloadStatus(true, 3, 10, 2048L, "clip.mp4", 7, 25, 4096L, 2, 12.5);

            DownloadStatus roundTripped = original.ToDto().ToDomain();

            Assert.Equal(original, roundTripped);
        }
    }
}
