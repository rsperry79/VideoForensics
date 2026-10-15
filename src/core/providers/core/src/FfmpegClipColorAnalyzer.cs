using System.Diagnostics;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>
    /// Samples about one frame per second with ffmpeg's signalstats filter and reads the per-frame saturation and luma.
    /// Samples rather than decoding every frame, so a long clip costs little.
    /// </summary>
    public sealed class FfmpegClipColorAnalyzer : IClipColorAnalyzer
    {
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
            psi.ArgumentList.Add("fps=1,signalstats,metadata=mode=print:file=-");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("null");
            psi.ArgumentList.Add("-");

            ProcessResult result = await ProcessRunner.RunAsync(psi, ProcessRunner.DefaultAnalysisTimeout, ct);
            return result.ExitCode == 0 ? SaturationAnalysis.ParseStats(result.StandardOutput) : null;
        }
    }
}
