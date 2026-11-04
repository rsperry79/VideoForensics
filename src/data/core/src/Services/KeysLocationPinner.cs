using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Keeps existing keys where they are when the Keys default moves. Before this, the keys default was the data root's
    /// Keys folder. Once the default follows the storage path, keys still on the data root would be looked for elsewhere,
    /// so they are pinned with a pointer until a Keys move relocates them. Run after the Keys reconcile, so a finished move
    /// is not pinned back.
    /// </summary>
    public static class KeysLocationPinner
    {
        public static void Pin(string dataRoot, string defaultKeysDir)
        {
            if (KeysLocationPointer.Read(dataRoot) is not null)
            {
                return;
            }

            string dataKeys = Path.Combine(dataRoot, "Keys");
            if (string.Equals(Path.GetFullPath(dataKeys), Path.GetFullPath(defaultKeysDir), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!Directory.Exists(dataKeys) || !Directory.EnumerateFiles(dataKeys).Any())
            {
                return;
            }

            KeysLocationPointer.Write(dataRoot, dataKeys);
        }
    }
}
