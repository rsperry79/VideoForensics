using System.Diagnostics;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>
    /// Samples about one frame per second with ffmpeg's signalstats filter and reads the per-frame saturation and luma.
    /// Samples rather than decoding every frame, so a long clip costs little.
    /// </summary>
    public sealed class FfmpegClipColorAnalyzer : IClipColorAnalyzer
    {
        /// <summary>
        /// Samples one frame a second, converts to 8-bit, then measures. signalstats reports luma in the stream's bit depth,
        /// so without the conversion a 10-bit clip would read on a 0-1023 scale and be misclassified.
        /// </summary>
        public const string FilterChain = "fps=1,format=yuv420p,signalstats,metadata=mode=print:file=-";

        /// <summary>
        /// The chain for a still image. A still has one frame, and fps=1 drops it, so the stats come out empty. Without fps the
        /// frame is measured directly.
        /// </summary>
        public const string StillFilterChain = "format=yuv420p,signalstats,metadata=mode=print:file=-";

        /// <summary>The filter chain for a file: stills use <see cref="StillFilterChain"/>, video uses <see cref="FilterChain"/>.</summary>
        public static string FilterFor(string mediaFilePath)
        {
            return Path.GetExtension(mediaFilePath).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" ? StillFilterChain : FilterChain;
        }

        private readonly string _ffmpegPath;

        /// <summary>Creates the analyzer. The ffmpeg path is resolved like the other ffmpeg tools.</summary>
        public FfmpegClipColorAnalyzer(string? ffmpegPath = null)
        {
            _ffmpegPath = FfmpegPathResolver.Resolve(ffmpegPath, "ffmpeg");
        }

        /// <inheritdoc />
        public async Task<ClipColorStats?> AnalyzeAsync(string mediaFilePath, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-nostdin");
            psi.ArgumentList.Add("-an");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(mediaFilePath);
            psi.ArgumentList.Add("-vf");
            psi.ArgumentList.Add(FilterFor(mediaFilePath));
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("null");
            psi.ArgumentList.Add("-");

            ProcessResult result = await ProcessRunner.RunAsync(psi, ProcessRunner.DefaultAnalysisTimeout, ct);
            return result.ExitCode == 0 ? SaturationAnalysis.ParseStats(result.StandardOutput) : null;
        }
    }
}
