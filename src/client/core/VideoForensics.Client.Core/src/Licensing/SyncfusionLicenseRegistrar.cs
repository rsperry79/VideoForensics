using System.Reflection;

using Microsoft.Extensions.Configuration;

using VideoForensics.Client.Core.Contracts;

namespace VideoForensics.Client.Core.Licensing
{
    /// <summary>
    /// Default <see cref="ISyncfusionLicenseRegistrar"/>. Resolves the Syncfusion license key from the file, configuration,
    /// environment, and baked-in sources, then registers the winner with the process-wide Syncfusion license provider.
    /// </summary>
    public class SyncfusionLicenseRegistrar : ISyncfusionLicenseRegistrar
    {
        /// <summary>
        /// Registers the Syncfusion Blazor license key if present. Priority order:
        /// 1. External license key file at %ProgramData%\VideoForensics\syncfusion-license.key (if present, overrides all)
        /// 2. SYNCFUSION_LICENSE_KEY environment variable (lets Visual Studio debug sessions pick up the key without a ProgramData file)
        /// 3. Baked-in license key from assembly metadata (set at CI build time via -p:SyncfusionLicenseKey, present in every officially distributed build)
        /// Falls back to unlicensed/dev-mode behavior if no source provides a key.
        /// </summary>
        public void RegisterIfPresent()
        {
            RegisterLicense(GetSyncfusionLicenseKeyFromEnvironment());
        }

        /// <summary>
        /// Registers the Syncfusion Blazor license key using the same priority order as the parameterless
        /// overload, but reads the env-var source from <paramref name="configuration"/> instead. Use this
        /// once the host's configuration is built (e.g. WebApplicationBuilder.Configuration), so user
        /// secrets (dev) and Azure App Settings / Key Vault references (prod) are honored. Configuration
        /// already includes environment variables, so SYNCFUSION_LICENSE_KEY set in the environment still works.
        /// </summary>
        /// <param name="configuration">Configuration to read SYNCFUSION_LICENSE_KEY from.</param>
        public void RegisterIfPresent(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            RegisterLicense(GetSyncfusionLicenseKeyFromConfiguration(configuration));
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
        /// (admin/IT override, always wins), then the configured key (SYNCFUSION_LICENSE_KEY from config/env), then the
        /// key baked into the assembly at CI build time. Blank or whitespace candidates count as absent so the next
        /// source is tried. Every returned key is trimmed. Returns null when no source has a key (unlicensed dev mode).
        /// Pure function so the priority rules can be unit tested without touching the process-wide license provider.
        /// </summary>
        /// <remarks>
        /// The file is first so an IT admin can override a baked-in key without rebuilding; the configured key sits
        /// above the baked-in key so a deployment can supply its own key without a rebuild.
        /// </remarks>
        internal static string? ResolveSyncfusionLicenseKey(string? fileKey, string? configuredKey, string? bakedInKey)
        {
            return NormalizeLicenseKey(fileKey) ?? NormalizeLicenseKey(configuredKey) ?? NormalizeLicenseKey(bakedInKey);
        }

        /// <summary>
        /// Reads the SYNCFUSION_LICENSE_KEY environment variable, trimmed. Returns null when unset or blank
        /// so the caller falls through to the baked-in key. Internal so tests can exercise it without
        /// touching the process-wide Syncfusion license provider.
        /// </summary>
        internal static string? GetSyncfusionLicenseKeyFromEnvironment()
        {
            return NormalizeLicenseKey(Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY"));
        }

        /// <summary>
        /// Reads SYNCFUSION_LICENSE_KEY from configuration, trimmed. Returns null when missing or blank.
        /// Internal so tests can exercise it with in-memory configuration.
        /// </summary>
        internal static string? GetSyncfusionLicenseKeyFromConfiguration(IConfiguration configuration)
        {
            return NormalizeLicenseKey(configuration["SYNCFUSION_LICENSE_KEY"]);
        }

        /// <summary>
        /// Reads %ProgramData%\VideoForensics\syncfusion-license.key, trimmed. Returns null when the file is missing or blank.
        /// </summary>
        private static string? ReadSyncfusionLicenseKeyFile()
        {
            string syncfusionLicenseKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "syncfusion-license.key");
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
