using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Runs stage-update.ps1 as the current user. Staging only writes the updates folder, which enable-dev-updates.ps1
    /// opened to this account once, so no elevation (and no UAC prompt) is involved.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class StageScriptRunner : IStageScriptRunner
    {
        /// <inheritdoc />
        public async Task<int?> RunAsync(string scriptPath, CancellationToken ct)
        {
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                Arguments = BuildArguments(scriptPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            try
            {
                using Process? process = Process.Start(startInfo);
                if (process is null)
                {
                    return null;
                }

                // Drain both streams while waiting: an unread redirected pipe fills up and blocks the script.
                Task<string> output = process.StandardOutput.ReadToEndAsync(ct);
                Task<string> errors = process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);

                foreach (string line in (await output + await errors).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    Debug.WriteLine($"stage-update: {line}");
                }

                return process.ExitCode;
            }
            catch (Win32Exception)
            {
                // powershell.exe could not be started.
                return null;
            }
        }

        /// <summary>
        /// Builds the powershell.exe arguments. -Build is always passed: staging is meant to deliver the current source,
        /// and without it the script fails when no publish folder exists yet.
        /// </summary>
        internal static string BuildArguments(string scriptPath)
        {
            return $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Build";
        }
    }
}
