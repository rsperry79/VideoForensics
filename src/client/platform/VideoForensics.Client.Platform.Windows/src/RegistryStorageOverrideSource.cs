using System;

using Microsoft.Win32;

using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Reads install-time storage overrides from <c>HKLM\SOFTWARE\VideoForensics</c>, written by the installer's
    /// optional "Data Directory" page. Returns null for categories with no registry value (Keys) and when the value is missing or unreadable.
    /// </summary>
    public sealed class RegistryStorageOverrideSource : IStorageOverrideSource
    {
        /// <inheritdoc />
        public string? GetOverride(StorageCategory category)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            string? valueName = StorageLocationProvider.GetRegistryValueName(category);
            if (valueName is null)
            {
                return null;
            }

            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(StorageLocationProvider.RegistryKey);
                return key?.GetValue(valueName) as string;
            }
            catch
            {
                // Permissions or a missing key: fall back to the default path, as the provider did before this seam.
                return null;
            }
        }
    }
}
