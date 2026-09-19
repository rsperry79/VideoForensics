using System.Collections.Concurrent;
using System.Diagnostics;

namespace VideoForensics.Providers.Core
{
    /// <summary>
    /// Auto-detects the best available ffmpeg hardware-acceleration method for video decoding.
    /// Probes candidate methods in priority order (platform-specific) and returns the first one that works.
    /// </summary>
    public static class FfmpegHardwareAccelDetector
    {
        private static readonly ConcurrentDictionary<string, Lazy<Task<string?>>> Cache =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Asynchronously detects the best available hardware-acceleration method for ffmpeg video decoding.
        /// Probes real device initialization (not just what the ffmpeg build claims to support) by running
        /// ffmpeg with each candidate hwaccel method in priority order. Returns the first working candidate,
        /// or null if no hardware acceleration is available (falling back to software decode).
        /// Results are cached per ffmpeg path for the process lifetime.
        /// </summary>
        /// <param name="ffmpegPath">Path to the ffmpeg executable (resolved via FfmpegPathResolver).</param>
        /// <param name="cancellationToken">Cancellation token for the detection operation.</param>
        /// <returns>
        /// The name of the detected hwaccel method (e.g., "cuda", "d3d11va", "vaapi"),
        /// or null if no hardware acceleration is available or the ffmpeg path does not exist.
        /// </returns>
        public static async Task<string?> DetectAsync(string ffmpegPath, CancellationToken cancellationToken = default)
        {
            var lazy = Cache.GetOrAdd(
                ffmpegPath,
                path => new Lazy<Task<string?>>(() => ProbeAsync(path))
            );

            return await lazy.Value.ConfigureAwait(false);
        }

        /// <summary>
        /// Synchronously detects the best available hardware-acceleration method for ffmpeg video decoding.
        /// Internally calls DetectAsync and blocks on the result; caching ensures the expensive probe
        /// only runs once per ffmpeg path per process lifetime.
        /// </summary>
        /// <param name="ffmpegPath">Path to the ffmpeg executable (resolved via FfmpegPathResolver).</param>
        /// <returns>
        /// The name of the detected hwaccel method (e.g., "cuda", "d3d11va", "vaapi"),
        /// or null if no hardware acceleration is available or the ffmpeg path does not exist.
        /// </returns>
        public static string? Detect(string ffmpegPath)
        {
            return DetectAsync(ffmpegPath).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Builds ffmpeg command-line arguments to enable hardware acceleration for video decoding.
        /// </summary>
        /// <param name="hwAccelMethod">The hardware acceleration method name (e.g., "cuda"), or null for software decode.</param>
        /// <returns>
        /// An array of strings to prepend before '-i' in the ffmpeg command line.
        /// Returns empty array if hwAccelMethod is null or empty, otherwise returns ["-hwaccel", hwAccelMethod].
        /// </returns>
        public static string[] BuildDecodeArgs(string? hwAccelMethod) =>
            string.IsNullOrEmpty(hwAccelMethod) ? Array.Empty<string>() : new[] { "-hwaccel", hwAccelMethod };

        /// <summary>
        /// Probes the ffmpeg executable to find the first available hardware-acceleration method.
        /// Tries candidates in priority order (Windows: d3d11va, cuda, qsv, dxva2; Linux: vaapi, cuda, qsv).
        /// For each candidate, runs ffmpeg with -init_hw_device and returns immediately upon success.
        /// </summary>
        private static async Task<string?> ProbeAsync(string ffmpegPath)
        {
            var candidates = OperatingSystem.IsWindows()
                ? new[] { "d3d11va", "cuda", "qsv", "dxva2" }
                : new[] { "vaapi", "cuda", "qsv" };

            foreach (var candidate in candidates)
            {
                if (await TryProbeMethod(ffmpegPath, candidate).ConfigureAwait(false))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to initialize a specific hardware-acceleration method by running ffmpeg with -init_hw_device.
        /// Returns true if the method is available (ffmpeg exits with code 0), false otherwise.
        /// Catches process-start exceptions and respects a 5-second timeout per probe.
        /// </summary>
        private static async Task<bool> TryProbeMethod(string ffmpegPath, string method)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-hide_banner -v error -init_hw_device {method}=probe -f lavfi -i nullsrc -frames:v 1 -f null -",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();

                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        try
                        {
                            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                            return process.ExitCode == 0;
                        }
                        catch (OperationCanceledException)
                        {
                            // Timeout: method is not available
                            try
                            {
                                process.Kill();
                            }
                            catch
                            {
                                // Ignore errors during cleanup
                            }

                            return false;
                        }
                    }
                }
            }
            catch
            {
                // Process failed to start or threw an exception: method is not available
                return false;
            }
        }
    }
}
