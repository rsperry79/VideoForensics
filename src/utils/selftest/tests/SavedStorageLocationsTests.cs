using VideoForensics.Client.Hosting;
using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    /// <summary>
    /// The selftest must load the storage locations saved in the database before it builds the key ring, as the WebApp
    /// does. Without this it reads the Keys folder from ProgramData and cannot decrypt the saved Ring refresh token.
    /// </summary>
    public sealed class SavedStorageLocationsTests
    {
        [Fact]
        public void Apply_CopiesSavedLocationsIntoStorageLocationStore()
        {
            string? keys = StorageLocationStore.KeysRoot;
            string? logs = StorageLocationStore.LogsRoot;
            string? defaultRoot = StorageLocationStore.DefaultRoot;
            bool read = StorageLocationStore.SavedSettingsRead;
            try
            {
                SavedStorageLocations.Apply(new SavedStorageSettings(@"G:\VideoForensics\Keys", @"G:\VideoForensics\Logs", @"G:\VideoForensics", DatabaseRead: true));

                Assert.Equal(@"G:\VideoForensics\Keys", StorageLocationStore.KeysRoot);
                Assert.Equal(@"G:\VideoForensics\Logs", StorageLocationStore.LogsRoot);
                Assert.Equal(@"G:\VideoForensics", StorageLocationStore.DefaultRoot);
                Assert.True(StorageLocationStore.SavedSettingsRead);
            }
            finally
            {
                StorageLocationStore.KeysRoot = keys;
                StorageLocationStore.LogsRoot = logs;
                StorageLocationStore.DefaultRoot = defaultRoot;
                StorageLocationStore.SavedSettingsRead = read;
            }
        }

        [Fact]
        public void Apply_NullSavedSettings_LeavesNullsSoProviderUsesDefaults()
        {
            string? keys = StorageLocationStore.KeysRoot;
            string? logs = StorageLocationStore.LogsRoot;
            string? defaultRoot = StorageLocationStore.DefaultRoot;
            bool read = StorageLocationStore.SavedSettingsRead;
            try
            {
                SavedStorageLocations.Apply(new SavedStorageSettings(null, null, null));

                Assert.Null(StorageLocationStore.KeysRoot);
                Assert.Null(StorageLocationStore.DefaultRoot);
                Assert.False(StorageLocationStore.SavedSettingsRead);
            }
            finally
            {
                StorageLocationStore.KeysRoot = keys;
                StorageLocationStore.LogsRoot = logs;
                StorageLocationStore.DefaultRoot = defaultRoot;
                StorageLocationStore.SavedSettingsRead = read;
            }
        }
    }
}
