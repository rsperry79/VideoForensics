using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Core.Helpers.Tests.Platform
{
    /// <summary>
    /// Tests that <see cref="StorageLocationProvider"/> takes its per-category overrides from an injected source.
    /// </summary>
    public class StorageOverrideSourceTests
    {
        private sealed class RecordingOverrideSource : IStorageOverrideSource
        {
            private readonly string _value;

            public RecordingOverrideSource(string value)
            {
                _value = value;
            }

            public string? GetOverride(StorageCategory category) => _value;
        }

        /// <summary>Verifies that a non-empty override from the source is returned as the category root.</summary>
        [Fact]
        public void GetDefaultRoot_OverrideSourceReturnsPath_ReturnsOverride()
        {
            var provider = new StorageLocationProvider(new RecordingOverrideSource(@"D:\Custom\Database"));

            Assert.Equal(@"D:\Custom\Database", provider.GetDefaultRoot(StorageCategory.Database));
        }

        /// <summary>Verifies that Keys ignores overrides, since it is deliberately not relocatable.</summary>
        [Fact]
        public void GetDefaultRoot_KeysCategory_IgnoresOverrideSource()
        {
            var provider = new StorageLocationProvider(new RecordingOverrideSource(@"D:\Custom\Keys"));

            Assert.NotEqual(@"D:\Custom\Keys", provider.GetDefaultRoot(StorageCategory.Keys));
        }

        /// <summary>Verifies that the null source leaves the default root untouched, so existing installs keep their paths.</summary>
        [Fact]
        public void GetDefaultRoot_NullOverrideSource_ReturnsDefaultRoot()
        {
            var withNull = new StorageLocationProvider(new NullStorageOverrideSource());
            var withDefault = new StorageLocationProvider(new NullStorageOverrideSource());

            Assert.Equal(withDefault.GetDefaultRoot(StorageCategory.Media), withNull.GetDefaultRoot(StorageCategory.Media));
        }
    }
}
