using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace VideoForensics.Client.Hosting.Updates
{
    /// <summary>Result of applying or rolling back an update.</summary>
    public sealed record UpdateApplyResult(bool Success, string Message);

    /// <summary>Copies a verified package over the install folder, with a backup so the change can be undone.</summary>
    public interface IUpdateApplier
    {
        /// <summary>
        /// Mirrors the package into <paramref name="installDir"/>: package files are written, and installed files the
        /// package no longer ships are removed unless they fall under <paramref name="keepList"/>. Everything replaced or
        /// removed is copied to <paramref name="backupDir"/> first. A failure part-way restores the backup.
        /// The package must already have passed <see cref="IUpdatePackageVerifier"/>.
        /// </summary>
        Task<UpdateApplyResult> ApplyAsync(string packageDir, UpdateManifest manifest, string installDir, string backupDir, IReadOnlyCollection<string> keepList, CancellationToken ct);

        /// <summary>Restores the install folder from a backup made by <see cref="ApplyAsync"/>. Safe to call after a crash mid-apply.</summary>
        Task<UpdateApplyResult> RollbackAsync(string installDir, string backupDir, CancellationToken ct);
    }

    /// <inheritdoc />
    public sealed class UpdateApplier : IUpdateApplier
    {
        private const string JournalFileName = "backup.json";
        private const string FilesFolderName = "files";
        private const string OldFileMarker = ".old-";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        private readonly ILogger<UpdateApplier> _logger;

        /// <summary>Creates the applier.</summary>
        public UpdateApplier(ILogger<UpdateApplier> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>What a backup holds: files to restore, and files the update added that rollback deletes.</summary>
        private sealed record BackupJournal(IReadOnlyList<string> Restore, IReadOnlyList<string> Added);

        /// <inheritdoc />
        public async Task<UpdateApplyResult> ApplyAsync(string packageDir, UpdateManifest manifest, string installDir, string backupDir, IReadOnlyCollection<string> keepList, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            ArgumentNullException.ThrowIfNull(keepList);

            // Resolve every destination first so an unsafe manifest fails before any file is touched.
            var packageFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (UpdateManifestFile file in manifest.Files)
            {
                if (!UpdatePathGuard.TryResolve(packageDir, file.Path, out string source)
                    || !UpdatePathGuard.TryResolve(installDir, file.Path, out string destination))
                {
                    _logger.LogError("Update apply failed: unsafe path '{Path}' in package", file.Path);
                    return new UpdateApplyResult(false, $"unsafe path '{file.Path}'");
                }

                packageFiles[Normalize(file.Path)] = source;
                _ = destination;
            }

            Directory.CreateDirectory(installDir);
            List<string> existing = ListInstalledFiles(installDir, keepList);
            List<string> toRemove = existing.Where(e => !packageFiles.ContainsKey(e)).ToList();
            List<string> toRestore = existing.ToList();
            List<string> toAdd = packageFiles.Keys.Where(k => !existing.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();

            try
            {
                if (Directory.Exists(backupDir))
                {
                    Directory.Delete(backupDir, recursive: true);
                }

                Directory.CreateDirectory(backupDir);

                foreach (string relative in toRestore)
                {
                    ct.ThrowIfCancellationRequested();
                    string backupPath = Path.Combine(backupDir, FilesFolderName, relative);
                    _ = Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                    File.Copy(Path.Combine(installDir, relative), backupPath, overwrite: true);
                }

                // The journal is written before the first change so a crash mid-apply can still be rolled back at startup.
                await File.WriteAllTextAsync(
                    Path.Combine(backupDir, JournalFileName),
                    JsonSerializer.Serialize(new BackupJournal(toRestore, toAdd), JsonOptions),
                    ct);

                foreach (KeyValuePair<string, string> pair in packageFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    _ = UpdatePathGuard.TryResolve(installDir, pair.Key, out string destination);
                    _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                    // Write beside the target then move, so a reader never sees a half-written file.
                    string temp = destination + ".update-tmp";
                    File.Copy(pair.Value, temp, overwrite: true);
                    ReplaceFile(temp, destination);
                }

                foreach (string relative in toRemove)
                {
                    ct.ThrowIfCancellationRequested();
                    SetAside(Path.Combine(installDir, relative), deleteFirst: true);
                }

                RemoveEmptyDirectories(installDir, keepList);
            }
            catch (Exception ex)
            {
                _logger.LogError("Update apply failed: {Error} ({ExceptionType}); restoring the previous version", ex.Message, ex.GetType().Name);
                UpdateApplyResult rollback = await RollbackAsync(installDir, backupDir, CancellationToken.None);
                return new UpdateApplyResult(false, $"apply failed: {ex.Message}; {rollback.Message}");
            }

            _logger.LogInformation("Applied update {Version}: {Written} files written, {Removed} removed", manifest.Version, packageFiles.Count, toRemove.Count);
            return new UpdateApplyResult(true, $"applied {manifest.Version}");
        }

        /// <inheritdoc />
        public async Task<UpdateApplyResult> RollbackAsync(string installDir, string backupDir, CancellationToken ct)
        {
            string journalPath = Path.Combine(backupDir, JournalFileName);
            if (!File.Exists(journalPath))
            {
                return new UpdateApplyResult(false, "no backup to restore");
            }

            try
            {
                BackupJournal? journal = JsonSerializer.Deserialize<BackupJournal>(await File.ReadAllTextAsync(journalPath, ct), JsonOptions);
                if (journal is null)
                {
                    return new UpdateApplyResult(false, "backup journal is unreadable");
                }

                foreach (string relative in journal.Added)
                {
                    if (UpdatePathGuard.TryResolve(installDir, relative, out string added) && File.Exists(added))
                    {
                        File.Delete(added);
                    }
                }

                foreach (string relative in journal.Restore)
                {
                    if (!UpdatePathGuard.TryResolve(installDir, relative, out string target))
                    {
                        continue;
                    }

                    _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(Path.Combine(backupDir, FilesFolderName, relative), target, overwrite: true);
                }

                RemoveEmptyDirectories(installDir, []);
                _logger.LogInformation("Restored the previous version from backup: {Restored} files", journal.Restore.Count);
                return new UpdateApplyResult(true, "restored previous version");
            }
            catch (Exception ex)
            {
                _logger.LogError("Update rollback failed: {Error} ({ExceptionType})", ex.Message, ex.GetType().Name);
                return new UpdateApplyResult(false, $"rollback failed: {ex.Message}");
            }
        }

        /// <summary>Deletes the <c>.old-*</c> files that locked binaries were renamed to during an earlier apply. Best effort.</summary>
        public static void DeleteLeftoverFiles(string installDir)
        {
            if (!Directory.Exists(installDir))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(installDir, "*" + OldFileMarker + "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Still in use by a running process; the next start tries again.
                }
            }
        }

        // The running exe and the dlls it has loaded cannot be overwritten or deleted on Windows, but they can be renamed.
        // Renaming the old file out of the way lets the new one take its name; the old one is deleted on a later start.
        private static void ReplaceFile(string temp, string destination)
        {
            try
            {
                File.Move(temp, destination, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SetAside(destination, deleteFirst: false);
                File.Move(temp, destination);
            }
        }

        private static void SetAside(string path, bool deleteFirst)
        {
            if (!File.Exists(path))
            {
                return;
            }

            if (deleteFirst)
            {
                try
                {
                    File.Delete(path);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Locked: fall through to the rename.
                }
            }

            File.Move(path, path + OldFileMarker + Guid.NewGuid().ToString("N"));
        }

        /// <summary>Lists installed files as normalized relative paths, skipping keep-list entries and reparse points.</summary>
        private static List<string> ListInstalledFiles(string installDir, IReadOnlyCollection<string> keepList)
        {
            var result = new List<string>();
            if (!Directory.Exists(installDir))
            {
                return result;
            }

            foreach (string file in Directory.EnumerateFiles(installDir, "*", SearchOption.AllDirectories))
            {
                string relative = Normalize(Path.GetRelativePath(installDir, file));
                if (IsKept(relative, keepList) || relative.Contains(OldFileMarker, StringComparison.Ordinal) || UpdatePathGuard.IsReparsePoint(file) || !UpdatePathGuard.TryResolve(installDir, relative, out _))
                {
                    continue;
                }

                result.Add(relative);
            }

            return result;
        }

        // A keep entry names a file or a top-level folder; matching on whole path segments stops "Desktop" keeping "DesktopTools".
        private static bool IsKept(string relative, IReadOnlyCollection<string> keepList) =>
            keepList.Any(k =>
            {
                string keep = Normalize(k);
                return relative.Equals(keep, StringComparison.OrdinalIgnoreCase)
                    || relative.StartsWith(keep + "/", StringComparison.OrdinalIgnoreCase);
            });

        private static string Normalize(string path) => path.Replace('\\', '/');

        private static void RemoveEmptyDirectories(string installDir, IReadOnlyCollection<string> keepList)
        {
            foreach (string dir in Directory.EnumerateDirectories(installDir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            {
                string relative = Normalize(Path.GetRelativePath(installDir, dir));
                if (!IsKept(relative, keepList) && !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
        }
    }
}
