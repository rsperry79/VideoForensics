using System;
using System.IO;
using System.Text.Json;

namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// Records where the Logs folder was relocated to, in a file under the data root. The saved Logs location lives inside
    /// the database, which the logger cannot read before it starts, so this file is the bootstrap pointer that startup reads.
    /// </summary>
    public static class LogsLocationPointer
    {
        public const string FileName = "logs-location.json";

        private sealed record PointerFile(string LogsRoot);

        /// <summary>Returns the recorded Logs root, or null if there is no usable pointer.</summary>
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
                return string.IsNullOrWhiteSpace(pointer?.LogsRoot) ? null : pointer.LogsRoot;
            }
            catch (JsonException)
            {
                // A corrupt pointer must not stop startup. Fall back to the default root.
                return null;
            }
        }

        /// <summary>Records the Logs root, creating the data root if needed.</summary>
        public static void Write(string dataRoot, string logsRoot)
        {
            _ = Directory.CreateDirectory(dataRoot);
            File.WriteAllText(
                Path.Combine(dataRoot, FileName),
                JsonSerializer.Serialize(new PointerFile(logsRoot)));
        }

        /// <summary>Removes the pointer, so startup uses the platform default Logs root again.</summary>
        public static void Clear(string dataRoot)
        {
            File.Delete(Path.Combine(dataRoot, FileName));
        }
    }
}
