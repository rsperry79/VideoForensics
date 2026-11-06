using System.Runtime.Versioning;
using Microsoft.Win32;

using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Keeps the Database bootstrap pointer in <c>HKLM\SOFTWARE\VideoForensics</c>, as the value
    /// <c>DatabaseLocationPointer</c>. Writing needs administrator rights, which the service already runs with.
    /// </summary>
    public sealed class RegistryDatabaseLocationPointerStore : IDatabaseLocationPointerStore
    {
        /// <summary>The registry value that holds the pointer. Separate from the installer's Database override value, which is the default.</summary>
        public const string ValueName = "DatabaseLocationPointer";

        /// <inheritdoc />
        public string? Read()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(StorageLocationProvider.RegistryKey);
                string? value = key?.GetValue(ValueName) as string;
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            catch
            {
                // A missing key or unreadable value means no pointer, so startup uses the default Database folder.
                return null;
            }
        }

        /// <inheritdoc />
        [SupportedOSPlatform("windows")]
        public void Write(string databaseRoot)
        {
            using RegistryKey key = Registry.LocalMachine.CreateSubKey(StorageLocationProvider.RegistryKey);
            key.SetValue(ValueName, databaseRoot, RegistryValueKind.String);
        }
    }
}
