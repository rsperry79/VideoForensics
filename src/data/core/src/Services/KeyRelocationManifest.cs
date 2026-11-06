using System.Security.Cryptography;
using System.Text.Json;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Finishes a Keys move after a restart. The move copies every key file and records each copy's SHA-256. The next start
    /// removes an original only when its copy hashes to the same value, so a bad copy never costs the only key. Reconcile
    /// must run before anything loads a key.
    /// </summary>
    public static class KeyRelocationManifest
    {
        /// <summary>Manifest file name, written into the new Keys folder.</summary>
        public const string FileName = "pending-key-removal.json";

        /// <summary>One copied key: where the original is, its name in the new folder, and the SHA-256 of the copy.</summary>
        public sealed record Entry(string SourcePath, string RelativePath, string Sha256);

        /// <summary>Writes the manifest that the next start verifies.</summary>
        public static void Write(string keysDir, IEnumerable<Entry> entries)
        {
            File.WriteAllText(Path.Combine(keysDir, FileName), JsonSerializer.Serialize(entries.ToList()));
        }

        /// <summary>The SHA-256 of a file, as lowercase hex.</summary>
        public static string HashOf(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        /// <summary>
        /// Removes each original whose copy hashes to the recorded value. An original whose copy is missing or does not match
        /// stays where it is, and the manifest stays for the next start.
        /// </summary>
        public static void Reconcile(string keysDir)
        {
            string manifestPath = Path.Combine(keysDir, FileName);
            if (!File.Exists(manifestPath))
            {
                return;
            }

            List<Entry> entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(manifestPath)) ?? [];
            bool allRemoved = true;
            foreach (Entry entry in entries)
            {
                allRemoved &= TryRemoveOriginal(keysDir, entry);
            }

            if (allRemoved)
            {
                File.Delete(manifestPath);
            }
        }

        /// <summary>Rolls back a Keys copy whose move did not finish: removes the copies and the manifest. Originals are never touched.</summary>
        public static void Discard(string keysDir)
        {
            string manifestPath = Path.Combine(keysDir, FileName);
            if (!File.Exists(manifestPath))
            {
                return;
            }

            List<Entry> entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(manifestPath)) ?? [];
            foreach (Entry entry in entries)
            {
                try
                {
                    File.Delete(Path.Combine(keysDir, entry.RelativePath));
                }
                catch (IOException)
                {
                    // A copy that cannot be removed is left behind. The original is still complete.
                }
            }

            File.Delete(manifestPath);
        }

        private static bool TryRemoveOriginal(string keysDir, Entry entry)
        {
            if (!File.Exists(entry.SourcePath))
            {
                return true;
            }

            string copy = Path.Combine(keysDir, entry.RelativePath);
            if (!File.Exists(copy) || !string.Equals(HashOf(copy), entry.Sha256, StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                File.Delete(entry.SourcePath);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
