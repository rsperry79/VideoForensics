using Microsoft.Extensions.Logging;

using System.Diagnostics;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Core;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Embeds a DB record GUID into a media file's container metadata (MP4 "comment" tag, JPG
    /// comment marker) by shelling out to ffmpeg/ffprobe, the same process-shelling pattern already
    /// used by VideoFrameExtractor in this codebase. ffmpeg cannot edit a container in place, so
    /// writing requires producing a new file and atomically replacing the original.
    /// </summary>
    public class FfmpegMediaMetadataTagger : IMediaMetadataTagger
    {
        private const string CommentPrefix = "vf-event-id:";
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;
        private readonly ILogger _logger;

        public FfmpegMediaMetadataTagger(ILogger logger, string? ffmpegPath = null, string? ffprobePath = null)
        {
            _logger = logger;
            _ffmpegPath = FfmpegPathResolver.Resolve(ffmpegPath, "ffmpeg");
            _ffprobePath = FfmpegPathResolver.Resolve(ffprobePath, "ffprobe");
        }

        public async Task<bool> TagEventIdAsync(string mediaFilePath, Guid eventId, CancellationToken ct)
        {
            string tmpPath = mediaFilePath + ".tmp";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = $"-y -i \"{mediaFilePath}\" -map_metadata 0 -metadata comment=\"{CommentPrefix}{eventId}\" -codec copy \"{tmpPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    _logger.LogWarning("Failed to start ffmpeg process for tagging {Path}", mediaFilePath);
                    return false;
                }

                await process.WaitForExitAsync(ct);

                if (process.ExitCode != 0 || !File.Exists(tmpPath))
                {
                    _logger.LogWarning("ffmpeg tagging failed for {Path}, exit code {Code}", mediaFilePath, process.ExitCode);
                    if (File.Exists(tmpPath))
                    {
                        File.Delete(tmpPath);
                    }

                    return false;
                }

                File.Delete(mediaFilePath);
                File.Move(tmpPath, mediaFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception tagging media file {Path}", mediaFilePath);
                if (File.Exists(tmpPath))
                {
                    try
                    { File.Delete(tmpPath); }
                    catch { }
                }

                return false;
            }
        }

        public async Task<Guid?> ReadEventIdAsync(string mediaFilePath, CancellationToken ct)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    Arguments = $"-v quiet -show_entries format_tags=comment -of default=noprint_wrappers=1:nokey=1 \"{mediaFilePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return null;
                }

                string output = (await process.StandardOutput.ReadToEndAsync(ct)).Trim();
                await process.WaitForExitAsync(ct);

                return output.StartsWith(CommentPrefix, StringComparison.Ordinal) &&
                    Guid.TryParse(output[CommentPrefix.Length..], out Guid id)
                    ? id
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception reading tagged metadata from {Path}", mediaFilePath);
                return null;
            }
        }
    }
}
