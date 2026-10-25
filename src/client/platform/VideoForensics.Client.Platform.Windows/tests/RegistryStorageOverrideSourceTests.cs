using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class RegistryStorageOverrideSourceTests
    {
        /// <summary>Verifies that Keys never has a registry override, on any platform, since keys are deliberately not relocatable.</summary>
        [Fact]
        public void GetOverride_KeysCategory_ReturnsNull()
        {
            var source = new RegistryStorageOverrideSource();

            Assert.Null(source.GetOverride(StorageCategory.Keys));
        }
    }
}
