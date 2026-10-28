using System.IO.Compression;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

namespace VideoForensics.Client.Hosting.Updates
{
    /// <summary>Result of staging a downloaded package.</summary>
    public sealed record UpdateStageResult(bool Success, string Message, string? StagedDir, string? Version);

    /// <summary>Turns a downloaded update zip into a verified package folder under the updates root.</summary>
    public interface IUpdateStager
    {
        /// <summary>
        /// Extracts the zip into a private staging folder, verifies it, and only then moves it to
        /// <c>&lt;updatesRoot&gt;/&lt;version&gt;</c>. A package folder that exists under the updates root is therefore always complete and verified.
        /// </summary>
        Task<UpdateStageResult> StageZipAsync(string zipPath, CancellationToken ct);
    }

    /// <inheritdoc />
    public sealed partial class UpdateStager : IUpdateStager
    {
        /// <summary>Folder under the updates root where extraction happens; ignored by the startup applier.</summary>
        public const string StagingFolderName = ".staging";

        // Guards against zip bombs: a real release is a few hundred MB.
        private const long MaxUncompressedBytes = 4L * 1024 * 1024 * 1024;

        private readonly string _updatesRoot;
        private readonly IUpdatePackageVerifier _verifier;
        private readonly bool _allowDevPackages;
        private readonly Func<string> _currentVersion;
        private readonly ILogger<UpdateStager> _logger;

        /// <summary>Creates the stager.</summary>
        public UpdateStager(string updatesRoot, IUpdatePackageVerifier verifier, bool allowDevPackages, Func<string> currentVersion, ILogger<UpdateStager> logger)
        {
            _updatesRoot = updatesRoot ?? throw new ArgumentNullException(nameof(updatesRoot));
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            _allowDevPackages = allowDevPackages;
            _currentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<UpdateStageResult> StageZipAsync(string zipPath, CancellationToken ct)
        {
            string staging = Path.Combine(_updatesRoot, StagingFolderName, Guid.NewGuid().ToString("N"));
            try
            {
                _ = Directory.CreateDirectory(staging);

                string? extractError = await ExtractAsync(zipPath, staging, ct);
                if (extractError is not null)
                {
                    return Fail(extractError);
                }

                UpdatePackageVerification verification = await _verifier.VerifyAsync(staging, _currentVersion(), _allowDevPackages, ct);
                if (!verification.IsValid || verification.Manifest is null)
                {
                    return Fail($"package rejected: {verification.Message}");
                }

                string version = verification.Manifest.Version;
                if (!SafeFolderName().IsMatch(version))
                {
                    return Fail($"package version '{version}' cannot be used as a folder name");
                }

                string finalDir = Path.Combine(_updatesRoot, version);
                if (Directory.Exists(finalDir))
                {
                    Directory.Delete(finalDir, recursive: true);
                }

                Directory.Move(staging, finalDir);
                _logger.LogInformation("Staged update {Version} for the next restart", version);
                return new UpdateStageResult(true, $"staged {version}", finalDir, version);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                return Fail($"staging failed: {ex.Message}");
            }
            finally
            {
                if (Directory.Exists(staging))
                {
                    TryDelete(staging);
                }
            }
        }

        /// <summary>Extracts every entry under <paramref name="target"/>, manifest last. Returns an error message, or null on success.</summary>
        private static async Task<string?> ExtractAsync(string zipPath, string target, CancellationToken ct)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);

            // The manifest is the "package is complete" marker, so it is written after everything it describes.
            List<ZipArchiveEntry> entries = archive.Entries
                .Where(e => e.FullName.Length > 0 && !e.FullName.EndsWith('/'))
                .OrderBy(e => string.Equals(e.FullName, UpdateManifest.FileName, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ToList();

            if (entries.Sum(e => e.Length) > MaxUncompressedBytes)
            {
                return "package is larger than the allowed size";
            }

            foreach (ZipArchiveEntry entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                if (!UpdatePathGuard.TryResolve(target, entry.FullName, out string destination))
                {
                    return $"package contains an unsafe path '{entry.FullName}'";
                }

                _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using Stream source = entry.Open();
                await using FileStream output = File.Create(destination);
                await source.CopyToAsync(output, ct);
            }

            return null;
        }

        private UpdateStageResult Fail(string message)
        {
            _logger.LogError("Update staging failed: {Error}", message);
            return new UpdateStageResult(false, message, null, null);
        }

        private static void TryDelete(string dir)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // Leftover staging folders are ignored by the applier and cleared on the next stage.
            }
        }

        [GeneratedRegex("^[0-9A-Za-z][0-9A-Za-z.+-]*$")]
        private static partial Regex SafeFolderName();
    }
}
