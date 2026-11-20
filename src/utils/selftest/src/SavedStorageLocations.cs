using VideoForensics.Client.Hosting;
using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Utils.SelfTest
{
    /// <summary>
    /// Copies the storage locations saved in the database into <see cref="StorageLocationStore"/>, the same step the WebApp
    /// takes before the key ring loads. Keys resolve through that store, so skipping this points the selftest at the
    /// wrong Keys folder and the saved Ring refresh token cannot be decrypted.
    /// </summary>
    internal static class SavedStorageLocations
    {
        /// <summary>Applies the saved locations. A null value means default, and <see cref="SavedStorageSettings.DatabaseRead"/>
        /// marks whether the database was actually read.</summary>
        internal static void Apply(SavedStorageSettings saved)
        {
            StorageLocationStore.KeysRoot = saved.KeysRoot;
            StorageLocationStore.LogsRoot = saved.LogsRoot;
            StorageLocationStore.DefaultRoot = saved.DefaultRoot;
            StorageLocationStore.SavedSettingsRead = saved.DatabaseRead;
        }
    }
}
