using System.Diagnostics;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Measures a clip's audio: clipping, noise floor, and silent gaps.</summary>
    public interface IAudioClipAnalyzer
    {
        /// <summary>Returns the audio stats, or null when the clip has no usable audio.</summary>
        Task<AudioClipStats?> AnalyzeAsync(string mediaFilePath, double? durationSeconds, CancellationToken ct);
    }

    /// <summary>Runs ffmpeg <c>astats</c> and <c>silencedetect</c> over a clip's audio track.</summary>
    public sealed class FfmpegAudioAnalyzer : IAudioClipAnalyzer
    {
        /// <summary>Silence below this level counts as silent. Ring audio is compressed, so this is a practical floor.</summary>
        public const string SilenceThreshold = "-50dB";

        private readonly string _ffmpegPath;

        /// <summary>Creates the analyzer. The ffmpeg path is resolved like the other ffmpeg tools.</summary>
        public FfmpegAudioAnalyzer(string? ffmpegPath = null)
        {
            _ffmpegPath = FfmpegPathResolver.Resolve(ffmpegPath, "ffmpeg");
        }

        /// <inheritdoc />
        public async Task<AudioClipStats?> AnalyzeAsync(string mediaFilePath, double? durationSeconds, CancellationToken ct)
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
            psi.ArgumentList.Add("-vn");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(mediaFilePath);
            psi.ArgumentList.Add("-af");
            // astats metadata goes to stdout; silencedetect logs its gaps to stderr.
            psi.ArgumentList.Add($"astats=metadata=1:reset=0,ametadata=mode=print:file=-,silencedetect=noise={SilenceThreshold}:d={AudioStatsParser.MinimumGapSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("null");
            psi.ArgumentList.Add("-");

            ProcessResult result = await ProcessRunner.RunAsync(psi, ProcessRunner.DefaultAnalysisTimeout, ct);
            return result.ExitCode == 0 ? AudioStatsParser.Parse(result.StandardOutput, result.StandardError, durationSeconds) : null;
        }
    }
}
