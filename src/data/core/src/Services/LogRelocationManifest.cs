using System.Text.Json;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Finishes a Logs move after a restart. The running server keeps its audit file open, so the move copies every file
    /// and records each copy's length here. The next start appends whatever the old file gained after its copy, then removes
    /// the old file. Reconcile must run before the logger opens the new folder, so the appended lines stay in order.
    /// </summary>
    public static class LogRelocationManifest
    {
        /// <summary>Manifest file name, written into the new logs folder.</summary>
        public const string FileName = "pending-log-removal.json";

        /// <summary>One copied log file: where the original is, its name in the new folder, and how many bytes the copy holds.</summary>
        public sealed record Entry(string SourcePath, string RelativePath, long CopiedLength);

        /// <summary>Writes the manifest that the next start reconciles.</summary>
        public static void Write(string logsDir, IEnumerable<Entry> entries)
        {
            File.WriteAllText(Path.Combine(logsDir, FileName), JsonSerializer.Serialize(entries.ToList()));
        }

        /// <summary>
        /// Appends bytes written to each original after its copy, then removes the original. An original that cannot be
        /// removed or no longer matches its copy stays where it is, and the manifest stays for the next start.
        /// </summary>
        public static void Reconcile(string logsDir)
        {
            string manifestPath = Path.Combine(logsDir, FileName);
            if (!File.Exists(manifestPath))
            {
                return;
            }

            List<Entry> entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(manifestPath)) ?? [];
            bool allRemoved = true;
            foreach (Entry entry in entries)
            {
                allRemoved &= TryReconcile(logsDir, entry);
            }

            if (allRemoved)
            {
                File.Delete(manifestPath);
            }
        }

        /// <summary>
        /// Rolls back a Logs copy whose move did not finish: removes the copies and the manifest. The originals are never
        /// touched, so the audit trail stays complete in the old folder.
        /// </summary>
        public static void Discard(string logsDir)
        {
            string manifestPath = Path.Combine(logsDir, FileName);
            if (!File.Exists(manifestPath))
            {
                return;
            }

            List<Entry> entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(manifestPath)) ?? [];
            foreach (Entry entry in entries)
            {
                try
                {
                    File.Delete(Path.Combine(logsDir, entry.RelativePath));
                }
                catch (IOException)
                {
                    // A copy that cannot be removed is left behind. The original is still complete.
                }
            }

            File.Delete(manifestPath);
        }

        private static bool TryReconcile(string logsDir, Entry entry)
        {
            if (!File.Exists(entry.SourcePath))
            {
                return true;
            }

            string target = Path.Combine(logsDir, entry.RelativePath);
            if (!File.Exists(target))
            {
                File.Copy(entry.SourcePath, target);
            }
            else if (new FileInfo(target).Length == entry.CopiedLength)
            {
                AppendTail(entry.SourcePath, target, entry.CopiedLength);
            }
            else
            {
                // The new copy is not what was copied, so it is not safe to append or to remove the original.
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

        private static void AppendTail(string source, string target, long copiedLength)
        {
            long sourceLength = new FileInfo(source).Length;
            if (sourceLength <= copiedLength)
            {
                return;
            }

            using FileStream input = File.OpenRead(source);
            _ = input.Seek(copiedLength, SeekOrigin.Begin);
            using FileStream output = new(target, FileMode.Append, FileAccess.Write);
            input.CopyTo(output);
        }
    }
}
