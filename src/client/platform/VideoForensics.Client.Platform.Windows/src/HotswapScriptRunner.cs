using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Runs hotswap-service.ps1 through UAC. The script needs administrator rights to stop and restart the service,
    /// so it must be started with the "runas" verb, which requires UseShellExecute (so its output is not captured).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class HotswapScriptRunner : IHotswapScriptRunner
    {
        /// <inheritdoc />
        public async Task<int?> RunAsync(string scriptPath, CancellationToken ct)
        {
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                Arguments = BuildArguments(scriptPath),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal,
            };

            try
            {
                using Process? process = Process.Start(startInfo);
                if (process is null)
                {
                    return null;
                }

                await process.WaitForExitAsync(ct);
                return process.ExitCode;
            }
            catch (Win32Exception)
            {
                // ERROR_CANCELLED (1223) when the UAC prompt is declined; other codes mean the process could not start.
                return null;
            }
        }

        /// <summary>
        /// Builds the powershell.exe arguments. -Build is always passed: the script otherwise looks for an existing
        /// publish folder and fails when none exists, and a hot-swap is meant to deploy the current source.
        /// </summary>
        internal static string BuildArguments(string scriptPath)
        {
            return $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Build";
        }
    }
}
