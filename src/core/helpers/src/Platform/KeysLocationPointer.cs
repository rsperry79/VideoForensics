using System;
using System.IO;
using System.Text.Json;

namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// Records where the Keys folder was relocated to, in a file under the data root. Startup reads this before any key is
    /// loaded, so the server and the tools opens the same key folder.
    /// </summary>
    public static class KeysLocationPointer
    {
        public const string FileName = "keys-location.json";

        private sealed record PointerFile(string KeysRoot);

        /// <summary>Returns the recorded Keys root, or null if there is no usable pointer.</summary>
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
                return string.IsNullOrWhiteSpace(pointer?.KeysRoot) ? null : pointer.KeysRoot;
            }
            catch (JsonException)
            {
                // A corrupt pointer must not stop startup. Fall back to the data root's Keys folder.
                return null;
            }
        }

        /// <summary>Records the Keys root, creating the data root if needed.</summary>
        public static void Write(string dataRoot, string keysRoot)
        {
            _ = Directory.CreateDirectory(dataRoot);
            File.WriteAllText(
                Path.Combine(dataRoot, FileName),
                JsonSerializer.Serialize(new PointerFile(keysRoot)));
        }

        /// <summary>Removes the pointer, so startup uses the data root's Keys folder again.</summary>
        public static void Clear(string dataRoot)
        {
            File.Delete(Path.Combine(dataRoot, FileName));
        }
    }
}
