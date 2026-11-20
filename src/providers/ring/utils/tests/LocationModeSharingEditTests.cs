using VideoForensics.Providers.Ring;

using Xunit;

namespace VideoForensics.Providers.Ring.Utils.Tests
{
    /// <summary>
    /// The sharing toggle must send only the field it changes. The sharing body is one boolean, so the
    /// captured value can be restored exactly; a wider body could overwrite settings the tester never read.
    /// </summary>
    public class LocationModeSharingEditTests
    {
        [Fact]
        public void Toggled_ReturnsOppositeOfCapturedValue()
        {
            Assert.False(LocationModeSharingEdit.Toggled(true));
            Assert.True(LocationModeSharingEdit.Toggled(false));
        }

        [Fact]
        public void Body_SendsOnlySharedUsersEnabled()
        {
            Assert.Equal("{\"sharedUsersEnabled\":false}", LocationModeSharingEdit.Body(false));
            Assert.Equal("{\"sharedUsersEnabled\":true}", LocationModeSharingEdit.Body(true));
        }
    }
}
