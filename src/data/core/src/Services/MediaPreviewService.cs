using System.Diagnostics;

using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Runs one external process and returns its result. This is the seam the preview service uses so tests can fake
    /// ffmpeg without a real binary. The default implementation calls <see cref="ProcessRunner"/>.
    /// </summary>
    public interface IPreviewProcessRunner
    {
        /// <summary>Runs the process within <paramref name="timeout"/>. Throws on timeout or cancellation.</summary>
        Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken ct);
    }

    /// <summary>Default <see cref="IPreviewProcessRunner"/> that delegates to <see cref="ProcessRunner"/>.</summary>
    internal sealed class DefaultPreviewProcessRunner : IPreviewProcessRunner
    {
        /// <inheritdoc />
        public Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken ct)
        {
            return ProcessRunner.RunAsync(startInfo, timeout, ct);
        }
    }

    /// <summary>
    /// Makes a 1 fps, 320 px wide, H.264 MP4 preview beside each video, for the evidence strip. The preview is written to
    /// a temp file and moved into place only after ffmpeg succeeds, so a partial file never sits at the final path.
    /// </summary>
    public sealed class MediaPreviewService : IMediaPreviewService
    {
        /// <summary>Preview width in pixels; height follows the aspect ratio (rounded to an even number by ffmpeg).</summary>
        public const int PreviewWidth = 320;

        /// <summary>
        /// Why: the preview is a sidecar of the clip, so it is named from the clip stem and stays with the file when the
        /// archive moves. The temp name adds ".tmp" after the extension, which is why the output format is forced to mp4.
        /// </summary>
        private const string PreviewSuffix = ".preview.mp4";

        private static readonly TimeSpan EncodeTimeout = ProcessRunner.DefaultAnalysisTimeout;

        private readonly IMediaItemRepository _items;
        private readonly IFfmpegToolLocation _tools;
        private readonly IPreviewProcessRunner _runner;
        private readonly IMediaPathResolver _paths;
        private readonly ILogger<MediaPreviewService> _logger;

        /// <summary>Creates the service. <paramref name="processRunner"/> defaults to the real ffmpeg runner when null.</summary>
        public MediaPreviewService(
            IMediaItemRepository items,
            IFfmpegToolLocation tools,
            ILogger<MediaPreviewService> logger,
            IPreviewProcessRunner? processRunner = null,
            IMediaPathResolver? paths = null)
        {
            _items = items;
            _tools = tools;
            _logger = logger;
            _runner = processRunner ?? new DefaultPreviewProcessRunner();
            _paths = paths ?? PassThroughMediaPathResolver.Instance;
        }

        /// <summary>File name of a clip's preview: the stem plus ".preview.mp4".</summary>
        public static string PreviewFileNameFor(string mediaFilePath)
        {
            return Path.GetFileNameWithoutExtension(mediaFilePath) + PreviewSuffix;
        }

        /// <summary>Full path of the preview beside the clip.</summary>
        public static string PreviewPathFor(string mediaFilePath)
        {
            string directory = Path.GetDirectoryName(mediaFilePath) ?? string.Empty;
            return Path.Combine(directory, PreviewFileNameFor(mediaFilePath));
        }

        /// <inheritdoc />
        public async Task<bool> EnsurePreviewAsync(MediaItem media, CancellationToken ct)
        {
            string source = await _paths.ToAbsoluteAsync(media.FilePath, ct);
            string preview = PreviewPathFor(source);
            if (IsNonEmptyFile(preview))
            {
                await RecordPathAsync(media, preview, ct);
                return true;
            }

            ct.ThrowIfCancellationRequested();

            string temp = preview + ".tmp";
            bool moved = false;
            try
            {
                DeleteQuietly(temp);

                string ffmpeg = FfmpegPathResolver.Resolve(null, "ffmpeg", _tools.BinDirectory);
                ProcessStartInfo startInfo = BuildStartInfo(ffmpeg, source, temp);
                ProcessResult result = await _runner.RunAsync(startInfo, EncodeTimeout, ct);

                if (result.ExitCode != 0)
                {
                    // ffmpeg writes the reason to stderr (for example, which file it could not open). Keep its last lines.
                    _logger.LogError(
                        "Preview generation failed for {FileName}: ffmpeg exited with code {ExitCode}: {FfmpegError}",
                        media.FileName, result.ExitCode, LastStderrLines(result.StandardError));
                    return false;
                }

                if (!IsNonEmptyFile(temp))
                {
                    _logger.LogError("Preview generation failed for {FileName}: ffmpeg produced no output", media.FileName);
                    return false;
                }

                File.Move(temp, preview, overwrite: true);
                moved = true;
                _logger.LogInformation("Preview generated for {FileName}", media.FileName);
                await RecordPathAsync(media, preview, ct);
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preview generation failed for {FileName}: {Message}", media.FileName, ex.Message);
                return false;
            }
            finally
            {
                if (!moved)
                {
                    DeleteQuietly(temp);
                }
            }
        }

        /// <summary>
        /// Why: the preview file is already on disk when this runs, so a failed save must not turn a good preview into a
        /// failure. The row is corrected on the next pass, when the existence check runs again.
        /// </summary>
        private async Task RecordPathAsync(MediaItem media, string preview, CancellationToken ct)
        {
            string stored = await _paths.ToStoredAsync(preview, ct);
            if (media.PreviewPath == stored)
            {
                return;
            }

            media.PreviewPath = stored;
            try
            {
                await _items.UpdateAsync(media, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not save preview path for {FileName}: {Message}", media.FileName, ex.Message);
            }
        }

        /// <summary>
        /// Builds the ffmpeg call. The filter is the timeline builder's fps-and-scale idea, reduced to a 320 px width with
        /// the aspect ratio kept. The output path is last so a fake runner can find it.
        /// </summary>
        private static ProcessStartInfo BuildStartInfo(string ffmpeg, string input, string output)
        {
            var startInfo = new ProcessStartInfo(ffmpeg);
            foreach (string arg in new[]
            {
                "-y", "-nostdin", "-i", input,
                "-vf", $"fps=1,scale={PreviewWidth}:-2:flags=lanczos,format=yuv420p",
                "-an", "-c:v", "libx264", "-preset", "veryfast", "-crf", "28",
                "-movflags", "+faststart", "-f", "mp4", output,
            })
            {
                startInfo.ArgumentList.Add(arg);
            }

            return startInfo;
        }

        /// <summary>The last three non-empty stderr lines, joined. The banner ffmpeg prints first is long and not useful.</summary>
        private static string LastStderrLines(string stderr)
        {
            string[] lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return string.Join(" | ", lines.TakeLast(3));
        }

        private static bool IsNonEmptyFile(string path)
        {
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        private static void DeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: a leftover temp file is overwritten by the next attempt, and this runs in a finally block.
            }
        }
    }
}
