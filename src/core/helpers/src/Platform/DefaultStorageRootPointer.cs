using System.IO;
using System.Text.Json;

namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// Records the chosen default storage root in a file under the machine data root. Every per-category default is built
    /// from this root, so the file is read before any category path is resolved. The file lives at the fixed data root, so
    /// it can be found even when the default root itself has changed.
    /// </summary>
    public static class DefaultStorageRootPointer
    {
        public const string FileName = "default-storage-root.json";

        private sealed record PointerFile(string DefaultRoot);

        /// <summary>Returns the recorded default root, or null if there is no usable pointer.</summary>
        public static string? Read(string dataRoot)
        {
            string path = Path.Combine(dataRoot, FileName);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                PointerFile? pointer = JsonSerializer.Deserialize<PointerFile>(File.ReadAllText(path));
                return string.IsNullOrWhiteSpace(pointer?.DefaultRoot) ? null : pointer.DefaultRoot;
            }
            catch (JsonException)
            {
                // A corrupt pointer must not stop startup. Fall back to the built-in default root.
                return null;
            }
        }

        /// <summary>Records the default root, creating the data root if needed.</summary>
        public static void Write(string dataRoot, string defaultRoot)
        {
            _ = Directory.CreateDirectory(dataRoot);
            File.WriteAllText(
                Path.Combine(dataRoot, FileName),
                JsonSerializer.Serialize(new PointerFile(defaultRoot)));
        }
    }
}
