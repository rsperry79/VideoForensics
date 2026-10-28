using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace VideoForensics.Client.Hosting.Updates
{
    /// <summary>What <see cref="StartupUpdateApplier"/> did at startup.</summary>
    public enum StartupUpdateOutcome
    {
        /// <summary>No staged package was waiting.</summary>
        None,

        /// <summary>A staged package was verified and applied; the new files are in place.</summary>
        Applied,

        /// <summary>A staged package failed verification and was set aside as rejected.</summary>
        Rejected,

        /// <summary>A verified package could not be applied; the previous version was restored.</summary>
        Failed,
    }

    /// <summary>Result of the startup apply, with a message for the log and the update status file.</summary>
    public sealed record StartupUpdateResult(StartupUpdateOutcome Outcome, string Message, string? Version);

    /// <summary>
    /// Applies a staged package before the host starts, while the previous process has exited and its files are unlocked.
    /// The staging folder is writable by a less-privileged user in dev mode, so the package is verified again here:
    /// nothing is trusted just because it sits in the updates folder.
    /// </summary>
    public sealed class StartupUpdateApplier
    {
        /// <summary>Folder under the updates root holding the backup of the last applied update.</summary>
        public const string BackupFolderName = "previous";

        /// <summary>Extension added to a staged folder that failed verification, so it is not retried.</summary>
        public const string RejectedSuffix = ".rejected";

        /// <summary>File under the updates root recording the last apply attempt, for the log and the UI.</summary>
        public const string StatusFileName = "status.json";

        /// <summary>Installed files and folders an update never removes (the client, and installer extras the package does not carry).</summary>
        public static readonly IReadOnlyCollection<string> DefaultKeepList =
        [
            "Desktop", "cloudflared.exe", "ffmpeg.exe", "ffprobe.exe", "ffmpeg-LICENSE.txt", "unins000.exe", "unins000.dat",
        ];

        private readonly IUpdatePackageVerifier _verifier;
        private readonly IUpdateApplier _applier;
        private readonly ILogger<StartupUpdateApplier> _logger;

        /// <summary>Creates the startup applier.</summary>
        public StartupUpdateApplier(IUpdatePackageVerifier verifier, IUpdateApplier applier, ILogger<StartupUpdateApplier> logger)
        {
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Finds the newest staged package, verifies it, and applies it. At most one package is applied per call.
        /// A package that fails verification is renamed with <see cref="RejectedSuffix"/>; an apply that fails
        /// is rolled back by <see cref="IUpdateApplier"/> and the package is rejected so it is not retried in a crash loop.
        /// </summary>
        public async Task<StartupUpdateResult> TryApplyStagedAsync(string updatesRoot, string installDir, string currentVersion, bool allowDevPackages, CancellationToken ct)
        {
            string? candidate = FindNewestStaged(updatesRoot);
            if (candidate is null)
            {
                return new StartupUpdateResult(StartupUpdateOutcome.None, "no staged update", null);
            }

            UpdatePackageVerification verification = await _verifier.VerifyAsync(candidate, currentVersion, allowDevPackages, ct);
            if (!verification.IsValid || verification.Manifest is null)
            {
                Reject(candidate);
                return await RecordAsync(updatesRoot, new StartupUpdateResult(StartupUpdateOutcome.Rejected, $"staged update rejected: {verification.Message}", verification.Manifest?.Version), ct);
            }

            UpdateManifest manifest = verification.Manifest;
            UpdateApplyResult apply = await _applier.ApplyAsync(candidate, manifest, installDir, Path.Combine(updatesRoot, BackupFolderName), DefaultKeepList, ct);
            if (!apply.Success)
            {
                Reject(candidate);
                return await RecordAsync(updatesRoot, new StartupUpdateResult(StartupUpdateOutcome.Failed, apply.Message, manifest.Version), ct);
            }

            Directory.Delete(candidate, recursive: true);
            return await RecordAsync(updatesRoot, new StartupUpdateResult(StartupUpdateOutcome.Applied, apply.Message, manifest.Version), ct);
        }

        // Folders starting with '.' (staging) and the backup folder are not packages; rejected ones were already tried.
        private static string? FindNewestStaged(string updatesRoot)
        {
            if (!Directory.Exists(updatesRoot))
            {
                return null;
            }

            return Directory.EnumerateDirectories(updatesRoot)
                .Where(d =>
                {
                    string name = Path.GetFileName(d);
                    return !name.StartsWith('.')
                        && !name.Equals(BackupFolderName, StringComparison.OrdinalIgnoreCase)
                        && !name.EndsWith(RejectedSuffix, StringComparison.OrdinalIgnoreCase)
                        && File.Exists(Path.Combine(d, UpdateManifest.FileName));
                })
                .OrderByDescending(d => ParseVersion(Path.GetFileName(d)))
                .FirstOrDefault();
        }

        private static Version ParseVersion(string name)
        {
            string trimmed = name.TrimStart('v');
            int cut = trimmed.IndexOfAny(['-', '+']);
            return Version.TryParse(cut < 0 ? trimmed : trimmed[..cut], out Version? v) ? v : new Version(0, 0);
        }

        private void Reject(string packageDir)
        {
            string target = packageDir + RejectedSuffix;
            try
            {
                if (Directory.Exists(target))
                {
                    Directory.Delete(target, recursive: true);
                }

                Directory.Move(packageDir, target);
            }
            catch (IOException ex)
            {
                _logger.LogError("Could not set aside the rejected update: {Error} ({ExceptionType})", ex.Message, ex.GetType().Name);
            }
        }

        private async Task<StartupUpdateResult> RecordAsync(string updatesRoot, StartupUpdateResult result, CancellationToken ct)
        {
            if (result.Outcome == StartupUpdateOutcome.Applied)
            {
                _logger.LogInformation("Startup update: {Message}", result.Message);
            }
            else
            {
                _logger.LogError("Startup update {Outcome}: {Message}", result.Outcome, result.Message);
            }

            try
            {
                var status = new { result.Outcome, result.Message, result.Version, AtUtc = DateTimeOffset.UtcNow };
                await File.WriteAllTextAsync(Path.Combine(updatesRoot, StatusFileName), JsonSerializer.Serialize(status), ct);
            }
            catch (IOException ex)
            {
                _logger.LogError("Could not write the update status file: {Error} ({ExceptionType})", ex.Message, ex.GetType().Name);
            }

            return result;
        }
    }
}
