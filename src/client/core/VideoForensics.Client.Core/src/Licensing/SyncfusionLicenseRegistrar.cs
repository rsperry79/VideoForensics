using System.Reflection;

using Microsoft.Extensions.Configuration;

using VideoForensics.Client.Core.Contracts;
using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.Core.Licensing
{
    /// <summary>
    /// Default <see cref="ISyncfusionLicenseRegistrar"/>. Resolves the Syncfusion license key from the file, configuration,
    /// and baked-in sources, then registers the winner with the process-wide Syncfusion license provider.
    /// </summary>
    public class SyncfusionLicenseRegistrar : ISyncfusionLicenseRegistrar
    {
        private const string LicenseKeyName = "SYNCFUSION_LICENSE_KEY";

        /// <summary>
        /// Registers the Syncfusion Blazor license key if present. Priority order:
        /// 1. External license key file at %ProgramData%\VideoForensics\syncfusion-license.key (if present, overrides all)
        /// 2. SYNCFUSION_LICENSE_KEY from Client.Core user secrets (developer machines: dotnet user-secrets set)
        /// 3. Baked-in license key from assembly metadata (set at CI build time via -p:SyncfusionLicenseKey, present in every officially distributed build)
        /// Falls back to unlicensed/dev-mode behavior if no source provides a key.
        /// </summary>
        public void RegisterIfPresent()
        {
            RegisterLicense(ResolveConfiguredKey(BuildLicenseConfiguration(typeof(SyncfusionLicenseRegistrar).Assembly)));
        }

        /// <summary>
        /// Resolves the key from the three sources in priority order and registers it. Logs nothing about the key value.
        /// </summary>
        private static void RegisterLicense(string? configuredKey)
        {
            string? key = ResolveSyncfusionLicenseKey(
                ReadSyncfusionLicenseKeyFile(),
                configuredKey,
                GetBakedInSyncfusionLicenseKey());
            if (key != null)
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(key);
            }
        }

        /// <summary>
        /// Picks the Syncfusion license key from the candidate sources, in priority order: the external key file
        /// (admin/IT override, always wins), then the configured key (user secrets), then the
        /// key baked into the assembly at CI build time. Blank or whitespace candidates count as absent so the next
        /// source is tried. Every returned key is trimmed. Returns null when no source has a key (unlicensed dev mode).
        /// Pure function so the priority rules can be unit tested without touching the process-wide license provider.
        /// </summary>
        /// <remarks>
        /// The file is first so an IT admin can override a baked-in key without rebuilding; the configured key sits
        /// above the baked-in key so a deployment or developer can supply its own key without a rebuild.
        /// </remarks>
        internal static string? ResolveSyncfusionLicenseKey(string? fileKey, string? configuredKey, string? bakedInKey)
        {
            return NormalizeLicenseKey(fileKey) ?? NormalizeLicenseKey(configuredKey) ?? NormalizeLicenseKey(bakedInKey);
        }

        /// <summary>
        /// Builds the configuration that supplies SYNCFUSION_LICENSE_KEY: Client.Core user secrets, identified by the
        /// assembly's UserSecretsId and optional so a missing file is not an error. No environment variables are read,
        /// so developers never need a machine-wide variable.
        /// </summary>
        /// <remarks>
        /// Client.Core is a library, so the host does not load its user secrets automatically; this method loads them
        /// explicitly. Taking the assembly as a parameter keeps the secrets source injectable in tests.
        /// </remarks>
        /// <param name="userSecretsAssembly">Assembly whose UserSecretsId identifies the secrets file to load.</param>
        internal static IConfiguration BuildLicenseConfiguration(Assembly userSecretsAssembly)
        {
            ArgumentNullException.ThrowIfNull(userSecretsAssembly);
            return new ConfigurationBuilder()
                .AddUserSecrets(userSecretsAssembly, optional: true)
                .Build();
        }

        /// <summary>
        /// Reads SYNCFUSION_LICENSE_KEY from configuration, trimmed. Returns null when missing or blank so the caller
        /// falls through to the baked-in key. Internal so tests can exercise it with in-memory configuration.
        /// </summary>
        internal static string? ResolveConfiguredKey(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            return NormalizeLicenseKey(configuration[LicenseKeyName]);
        }

        /// <summary>
        /// Reads %ProgramData%\VideoForensics\syncfusion-license.key, trimmed. Returns null when the file is missing or blank.
        /// </summary>
        private static string? ReadSyncfusionLicenseKeyFile()
        {
            string syncfusionLicenseKeyPath = Path.Combine(new StorageLocationProvider().GetDataRoot(), "syncfusion-license.key");
            return File.Exists(syncfusionLicenseKeyPath) ? NormalizeLicenseKey(File.ReadAllText(syncfusionLicenseKeyPath)) : null;
        }

        /// <summary>
        /// Reads the license key baked into Client.Core's assembly metadata at CI build time, trimmed. Returns null when absent.
        /// Reads from this class's own assembly, not the calling host, so the key travels with Client.Core.dll.
        /// </summary>
        private static string? GetBakedInSyncfusionLicenseKey()
        {
            return NormalizeLicenseKey(typeof(SyncfusionLicenseRegistrar).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "SyncfusionLicenseKey")?.Value);
        }

        /// <summary>
        /// Trims a candidate key and maps blank to null, so "absent" is one concept everywhere.
        /// </summary>
        private static string? NormalizeLicenseKey(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
