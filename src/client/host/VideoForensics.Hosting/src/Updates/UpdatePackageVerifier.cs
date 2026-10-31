using System.Security.Cryptography;

namespace VideoForensics.Client.Hosting.Updates
{
    /// <summary>Outcome category of verifying a staged update package.</summary>
    public enum UpdatePackageStatus
    {
        /// <summary>The package is complete, intact and acceptable to apply.</summary>
        Valid,

        /// <summary>No manifest.json in the package folder; the package is incomplete or still being written.</summary>
        ManifestMissing,

        /// <summary>manifest.json could not be parsed or has no version or files.</summary>
        ManifestInvalid,

        /// <summary>A manifest path is rooted, escapes the package folder, or passes through a reparse point.</summary>
        UnsafePath,

        /// <summary>A file listed in the manifest is not in the package.</summary>
        FileMissing,

        /// <summary>A file's size differs from the manifest.</summary>
        SizeMismatch,

        /// <summary>A file's SHA-256 differs from the manifest.</summary>
        HashMismatch,

        /// <summary>The package version is not newer than the running version.</summary>
        VersionNotNewer,

        /// <summary>The package is unsigned or its signature did not verify, and dev packages are not allowed.</summary>
        SignatureInvalid,
    }

    /// <summary>Result of <see cref="IUpdatePackageVerifier.VerifyAsync"/>. <paramref name="Manifest"/> is set when it parsed.</summary>
    public sealed record UpdatePackageVerification(UpdatePackageStatus Status, string Message, UpdateManifest? Manifest)
    {
        /// <summary>True when <see cref="Status"/> is <see cref="UpdatePackageStatus.Valid"/>.</summary>
        public bool IsValid => Status == UpdatePackageStatus.Valid;
    }

    /// <summary>Verifies the signature on a staged package (Authenticode on the exes and a signed manifest in release builds).</summary>
    public interface IPackageSignatureVerifier
    {
        /// <summary>Returns true when the package's signature is valid and from the expected publisher.</summary>
        Task<bool> VerifyAsync(string packageDir, UpdateManifest manifest, CancellationToken ct);
    }

    /// <summary>Verifies a staged update package before anything in it is copied or run.</summary>
    public interface IUpdatePackageVerifier
    {
        /// <summary>
        /// Checks the manifest, every file's size and hash, path safety, the version against <paramref name="currentVersion"/>,
        /// and the signature. Unsigned packages and same-version packages pass only when <paramref name="allowDevPackages"/> is true.
        /// </summary>
        Task<UpdatePackageVerification> VerifyAsync(string packageDir, string currentVersion, bool allowDevPackages, CancellationToken ct);
    }

    /// <inheritdoc />
    public sealed class UpdatePackageVerifier : IUpdatePackageVerifier
    {
        private readonly IPackageSignatureVerifier? _signatureVerifier;

        /// <summary>Creates the verifier. With a null <paramref name="signatureVerifier"/> only dev packages can be valid.</summary>
        public UpdatePackageVerifier(IPackageSignatureVerifier? signatureVerifier = null)
        {
            _signatureVerifier = signatureVerifier;
        }

        /// <inheritdoc />
        public async Task<UpdatePackageVerification> VerifyAsync(string packageDir, string currentVersion, bool allowDevPackages, CancellationToken ct)
        {
            string manifestPath = Path.Combine(packageDir, UpdateManifest.FileName);
            if (!File.Exists(manifestPath))
            {
                return Fail(UpdatePackageStatus.ManifestMissing, "manifest.json not found", null);
            }

            UpdateManifest? manifest = UpdateManifest.TryParse(await File.ReadAllTextAsync(manifestPath, ct));
            if (manifest is null || manifest.Files.Count == 0)
            {
                return Fail(UpdatePackageStatus.ManifestInvalid, "manifest.json is not a valid manifest", null);
            }

            if (!TryCompareVersions(manifest.Version, currentVersion, out int comparison))
            {
                return Fail(UpdatePackageStatus.ManifestInvalid, $"version '{manifest.Version}' or '{currentVersion}' is not a valid version", manifest);
            }

            if (comparison < 0 || (comparison == 0 && !allowDevPackages))
            {
                return Fail(UpdatePackageStatus.VersionNotNewer, $"package version {manifest.Version} is not newer than {currentVersion}", manifest);
            }

            foreach (UpdateManifestFile file in manifest.Files)
            {
                ct.ThrowIfCancellationRequested();

                if (!UpdatePathGuard.TryResolve(packageDir, file.Path, out string fullPath))
                {
                    return Fail(UpdatePackageStatus.UnsafePath, $"unsafe path '{file.Path}'", manifest);
                }

                if (!File.Exists(fullPath))
                {
                    return Fail(UpdatePackageStatus.FileMissing, $"file '{file.Path}' is missing", manifest);
                }

                if (new FileInfo(fullPath).Length != file.Size)
                {
                    return Fail(UpdatePackageStatus.SizeMismatch, $"file '{file.Path}' has a different size than the manifest", manifest);
                }

                if (!string.Equals(await HashAsync(fullPath, ct), file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Fail(UpdatePackageStatus.HashMismatch, $"file '{file.Path}' does not match its hash", manifest);
                }
            }

            if (!allowDevPackages)
            {
                bool signed = _signatureVerifier is not null && await _signatureVerifier.VerifyAsync(packageDir, manifest, ct);
                if (!signed)
                {
                    return Fail(UpdatePackageStatus.SignatureInvalid, "package signature is missing or invalid", manifest);
                }
            }

            return new UpdatePackageVerification(UpdatePackageStatus.Valid, "package verified", manifest);
        }

        /// <summary>Computes the lowercase hex SHA-256 of a file.</summary>
        internal static async Task<string> HashAsync(string path, CancellationToken ct)
        {
            await using FileStream stream = File.OpenRead(path);
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
        }

        // Pre-release suffixes ("1.2.3-testing.4") are dropped: only the numeric base is compared, as UpdateCheckService does.
        private static bool TryCompareVersions(string left, string right, out int comparison)
        {
            comparison = 0;
            if (!Version.TryParse(BaseVersion(left), out Version? l) || !Version.TryParse(BaseVersion(right), out Version? r))
            {
                return false;
            }

            comparison = l.CompareTo(r);
            return true;
        }

        private static string BaseVersion(string version)
        {
            string trimmed = version.TrimStart('v');
            int cut = trimmed.IndexOfAny(['-', '+']);
            return cut < 0 ? trimmed : trimmed[..cut];
        }

        private static UpdatePackageVerification Fail(UpdatePackageStatus status, string message, UpdateManifest? manifest) =>
            new(status, message, manifest);
    }
}
