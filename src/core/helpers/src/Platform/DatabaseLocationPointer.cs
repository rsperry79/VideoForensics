using System.IO;
using System.Text.Json;

namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// Records where the Database folder was relocated to, in a file under the data root.
    /// The saved Database location lives inside the database, so startup cannot read it before opening
    /// the database. This file is the bootstrap pointer that every process reads first.
    /// </summary>
    public static class DatabaseLocationPointer
    {
        public const string FileName = "database-location.json";

        private sealed record PointerFile(string DatabaseRoot);

        /// <summary>Returns the recorded Database root, or null if there is no usable pointer.</summary>
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
                return string.IsNullOrWhiteSpace(pointer?.DatabaseRoot) ? null : pointer.DatabaseRoot;
            }
            catch (JsonException)
            {
                // A corrupt pointer must not stop startup. Fall back to the default root.
                return null;
            }
        }

        /// <summary>Records the Database root, creating the data root if needed.</summary>
        public static void Write(string dataRoot, string databaseRoot)
        {
            _ = Directory.CreateDirectory(dataRoot);
            File.WriteAllText(
                Path.Combine(dataRoot, FileName),
                JsonSerializer.Serialize(new PointerFile(databaseRoot)));
        }
    }
}
