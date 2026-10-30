using System;

using Microsoft.Win32;

namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// Reads the Windows <c>MachineGuid</c> from the registry. Returns null on other platforms, or when the value is missing or unreadable.
    /// </summary>
    /// <remarks>
    /// This is the default source used when a consumer is not given one, so its result is the same as before the seam existed.
    /// A platform head can pass its own <see cref="IMachineGuidSource"/> instead.
    /// </remarks>
    public sealed class RegistryMachineGuidSource : IMachineGuidSource
    {
        /// <inheritdoc />
        public string? ReadMachineGuid()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return key?.GetValue("MachineGuid")?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
