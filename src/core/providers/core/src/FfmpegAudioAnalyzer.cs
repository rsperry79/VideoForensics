using FFMpegCore;
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
            string filter = $"astats=metadata=1:reset=0,ametadata=mode=print:file=-,silencedetect=noise={SilenceThreshold}:d={AudioStatsParser.MinimumGapSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            FFMpegArgumentProcessor processor = FFMpegArguments
                .FromFileInput(mediaFilePath, true, o => o.WithCustomArgument("-vn"))
                .OutputToFile("-", true, o => o.WithCustomArgument($"-af {filter} -f null"));
            FfmpegCapture.CaptureResult result = await FfmpegCapture.RunAsync(processor, ProcessRunner.DefaultAnalysisTimeout, ct);
            return result.Success ? AudioStatsParser.Parse(result.Stdout, result.Stderr, durationSeconds) : null;
        }
    }
}
