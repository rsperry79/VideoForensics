using FFMpegCore;
using Microsoft.Extensions.Logging;

using System.Diagnostics;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Providers.Core;

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
        private static readonly TimeSpan TagTimeout = TimeSpan.FromHours(1);
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;
        private readonly ILogger _logger;

        public FfmpegMediaMetadataTagger(ILogger logger, string? ffmpegPath = null, string? ffprobePath = null, string? binDirectory = null)
        {
            _logger = logger;
            _ffmpegPath = FfmpegPathResolver.Resolve(ffmpegPath, "ffmpeg", binDirectory);
            _ffprobePath = FfmpegPathResolver.Resolve(ffprobePath, "ffprobe", binDirectory);
            FfmpegCapture.UseBinaryFolderOf(_ffmpegPath);
        }

        public async Task<bool> TagEventIdAsync(string mediaFilePath, Guid eventId, CancellationToken ct)
        {
            if (IsJpeg(mediaFilePath))
            {
                return await TagJpegAsync(mediaFilePath, eventId, ct);
            }

            string tmpPath = BuildTempPath(mediaFilePath);
            try
            {
                // Copy every stream and metadata entry, adding the comment. The copy goes to a temp file that replaces the original.
                FFMpegArgumentProcessor processor = FFMpegArguments
                    .FromFileInput(mediaFilePath, true)
                    .OutputToFile(tmpPath, true, o => o.WithCustomArgument($"-map_metadata 0 -metadata comment={CommentPrefix}{eventId} -codec copy"));
                // A failed run or a missing binary throws here and is logged as an error by the catch below.
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TagTimeout);
                _ = await processor.CancellableThrough(timeoutCts.Token).ProcessAsynchronously(throwOnError: true);

                if (!File.Exists(tmpPath))
                {
                    _logger.LogWarning("ffmpeg tagging produced no output for {Path}", mediaFilePath);
                    return false;
                }

                File.Delete(mediaFilePath);
                File.Move(tmpPath, mediaFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception tagging media file {Path}", mediaFilePath);
                if (File.Exists(tmpPath))
                {
                    try
                    { File.Delete(tmpPath); }
                    catch { }
                }

                return false;
            }
        }


        private static bool IsJpeg(string mediaFilePath)
        {
            string ext = Path.GetExtension(mediaFilePath);
            return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Temp file next to the original that keeps its extension ("clip.mp4" -> "clip.tmp.mp4").
        /// Why: ffmpeg infers the output muxer from the extension, and "clip.mp4.tmp" is rejected with exit -22.
        /// </summary>
        private static string BuildTempPath(string mediaFilePath)
        {
            string ext = Path.GetExtension(mediaFilePath);
            return mediaFilePath[..^ext.Length] + ".tmp" + ext;
        }

        /// <summary>
        /// Why: ffmpeg writes no comment into JPEG output, so JPEG tagging writes the COM segment directly.
        /// The temp file is written first and moved over the original, so a failure never leaves a half-written snapshot.
        /// </summary>
        private async Task<bool> TagJpegAsync(string mediaFilePath, Guid eventId, CancellationToken ct)
        {
            string tmpPath = BuildTempPath(mediaFilePath);
            try
            {
                byte[] original = await File.ReadAllBytesAsync(mediaFilePath, ct);
                string comment = CommentPrefix + eventId;
                if (JpegCommentSegment.TryRead(original) == comment)
                {
                    return true;
                }

                byte[] tagged = JpegCommentSegment.Insert(original, comment);
                await File.WriteAllBytesAsync(tmpPath, tagged, ct);
                File.Move(tmpPath, mediaFilePath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception tagging JPEG {Path}", mediaFilePath);
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
            if (IsJpeg(mediaFilePath))
            {
                return await ReadJpegEventIdAsync(mediaFilePath, ct);
            }

            try
            {
                IMediaAnalysis analysis = await FFProbe.AnalyseAsync(mediaFilePath, BuildFfprobeOptions(), ct);
                if (analysis.Format.Tags is null || !analysis.Format.Tags.TryGetValue("comment", out string? comment))
                {
                    return null;
                }

                return comment.StartsWith(CommentPrefix, StringComparison.Ordinal) &&
                    Guid.TryParse(comment[CommentPrefix.Length..], out Guid id)
                    ? id
                    : null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception reading tagged metadata from {Path}", mediaFilePath);
                return null;
            }
        }

        /// <summary>FFMpegCore takes a folder for its binaries. A bare name (found on PATH) leaves the default options in place.</summary>
        private FFOptions? BuildFfprobeOptions()
        {
            string? folder = Path.GetDirectoryName(_ffprobePath);
            return string.IsNullOrEmpty(folder) ? null : new FFOptions { BinaryFolder = folder };
        }


        private async Task<Guid?> ReadJpegEventIdAsync(string mediaFilePath, CancellationToken ct)
        {
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(mediaFilePath, ct);
                string? comment = JpegCommentSegment.TryRead(bytes);

                return comment is not null &&
                    comment.StartsWith(CommentPrefix, StringComparison.Ordinal) &&
                    Guid.TryParse(comment[CommentPrefix.Length..], out Guid id)
                    ? id
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception reading JPEG comment from {Path}", mediaFilePath);
                return null;
            }
        }

    }
}
