using FFMpegCore;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Analyzes the bitrate of a downloaded media file.</summary>
    public interface IMediaBitrateAnalyzer
    {
        /// <summary>Returns the analysis, or null when the file cannot be analyzed.</summary>
        Task<MediaBitrateAnalysis?> AnalyzeAsync(string mediaFilePath, long fileSizeBytes, CancellationToken ct);
    }

    /// <summary>
    /// Reads a downloaded file's duration and video bitrate through FFMpegCore's ffprobe wrapper. The ffprobe path is resolved
    /// like the other ffmpeg tools, and its folder is handed to FFMpegCore so the same binary runs.
    /// </summary>
    public sealed class FfprobeMediaBitrateAnalyzer : IMediaBitrateAnalyzer
    {
        private readonly string _ffprobePath;

        /// <summary>Creates the analyzer. The ffprobe path is resolved like the other ffmpeg tools.</summary>
        public FfprobeMediaBitrateAnalyzer(string? ffprobePath = null)
        {
            _ffprobePath = FfmpegPathResolver.Resolve(ffprobePath, "ffprobe");
        }

        /// <inheritdoc />
        public async Task<MediaBitrateAnalysis?> AnalyzeAsync(string mediaFilePath, long fileSizeBytes, CancellationToken ct)
        {
            try
            {
                IMediaAnalysis analysis = await FFProbe.AnalyseAsync(mediaFilePath, BuildOptions(), ct);
                return MediaBitrateAnalysis.FromProbe(analysis.Duration, analysis.PrimaryVideoStream?.BitRate, fileSizeBytes);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // A file ffprobe cannot read is reported as not analyzable, the same as a non-zero exit before.
                return null;
            }
        }

        /// <summary>FFMpegCore takes a folder, not a file. A bare name (found on PATH) leaves the default options in place.</summary>
        private FFOptions? BuildOptions()
        {
            string? folder = Path.GetDirectoryName(_ffprobePath);
            return string.IsNullOrEmpty(folder) ? null : new FFOptions { BinaryFolder = folder };
        }
    }
}
