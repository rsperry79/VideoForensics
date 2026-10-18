using System.Diagnostics;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>
    /// Builds a 1 fps H.264 MP4 from timed still images by driving ffmpeg's concat demuxer.
    /// Each still is shown for its hold, rounded to whole seconds (minimum 1 second), so the
    /// output frame count equals the sum of those whole-second holds. Stills are ordered by
    /// <see cref="TimelineStill.CapturedAt"/>. All frames are scaled and centre-padded to the
    /// bounding box of the inputs (largest width by largest height), rounded up to even
    /// dimensions because libx264 with yuv420p rejects odd sizes.
    /// </summary>
    public sealed class FfmpegTimelineVideoBuilder : ITimelineVideoBuilder
    {
        /// <summary>Default wall-clock budget for one ffmpeg encode before the process tree is killed.</summary>
        public static readonly TimeSpan DefaultTimeout = ProcessRunner.DefaultAnalysisTimeout;

        private readonly ILogger<FfmpegTimelineVideoBuilder> _logger;
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;
        private readonly TimeSpan _timeout;

        /// <summary>Creates a builder.</summary>
        /// <param name="logger">Logger for success and failure reporting.</param>
        /// <param name="ffmpegPath">
        /// Explicit ffmpeg executable path. When null, <see cref="FfmpegPathResolver"/> picks the bundled or PATH binary.
        /// ffprobe is looked up beside an explicit ffmpeg path (same name with "ffprobe" in place of "ffmpeg").
        /// </param>
        /// <param name="timeout">Time limit for each ffmpeg or ffprobe run; defaults to <see cref="DefaultTimeout"/>.</param>
        public FfmpegTimelineVideoBuilder(ILogger<FfmpegTimelineVideoBuilder> logger, string? ffmpegPath = null, TimeSpan? timeout = null)
        {
            _logger = logger;
            _ffmpegPath = FfmpegPathResolver.Resolve(ffmpegPath, "ffmpeg");
            _ffprobePath = string.IsNullOrWhiteSpace(ffmpegPath)
                ? FfmpegPathResolver.Resolve(null, "ffprobe")
                : DeriveFfprobePath(ffmpegPath);
            _timeout = timeout ?? DefaultTimeout;
        }

        /// <inheritdoc />
        public async Task<TimelineVideoResult> BuildAsync(TimelineVideoRequest request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);
            IReadOnlyList<TimelineStill> stills = ValidateAndOrder(request);
            string outputPath = Path.GetFullPath(request.OutputPath);

            ct.ThrowIfCancellationRequested();

            int frameCount = stills.Sum(s => WholeSecondHold(s.Hold));

            (bool sized, int width, int height, string? sizeError) = await TryProbeTargetSizeAsync(stills, ct);
            if (!sized)
            {
                _logger.LogError("Timeline video build could not read still dimensions for {StillCount} stills: {Error}", stills.Count, sizeError);
                return new TimelineVideoResult(false, null, sizeError, 0);
            }

            string workDir = Path.Combine(Path.GetTempPath(), "timeline-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(workDir);
                string listPath = Path.Combine(workDir, "stills.txt");
                await File.WriteAllTextAsync(listPath, BuildConcatList(stills), ct);

                string? outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                string filter =
                    $"fps=1,scale={width}:{height}:force_original_aspect_ratio=decrease," +
                    $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2";
                // -t caps the output at the total hold. The repeated last entry in the concat list makes ffmpeg
                // emit that still for its full hold again; without the cap the video runs one hold too long.
                string totalSeconds = frameCount.ToString(CultureInfo.InvariantCulture);
                foreach (string arg in new[]
                {
                    "-f", "concat", "-safe", "0", "-i", listPath,
                    "-vf", filter,
                    "-c:v", "libx264", "-pix_fmt", "yuv420p",
                    "-fps_mode", "cfr", "-g", "1", "-an", "-t", totalSeconds, "-y", outputPath
                })
                {
                    startInfo.ArgumentList.Add(arg);
                }

                ProcessResult run;
                try
                {
                    run = await ProcessRunner.RunAsync(startInfo, _timeout, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Covers timeouts and a missing or unstartable ffmpeg binary: report, do not throw.
                    _logger.LogError(ex, "Timeline video build could not run ffmpeg for {StillCount} stills", stills.Count);
                    return new TimelineVideoResult(false, null, ex.Message, 0);
                }

                if (run.ExitCode != 0 || !File.Exists(outputPath))
                {
                    _logger.LogError(
                        "Timeline video build failed for {StillCount} stills, exit code {Code}: {Stderr}",
                        stills.Count, run.ExitCode, run.StandardError);
                    return new TimelineVideoResult(false, null, $"ffmpeg exited with code {run.ExitCode}.", 0);
                }

                _logger.LogInformation(
                    "Built timeline video from {StillCount} stills: {FrameCount} frames at {Width}x{Height}",
                    stills.Count, frameCount, width, height);
                return new TimelineVideoResult(true, outputPath, null, frameCount);
            }
            finally
            {
                TryDeleteDirectory(workDir);
            }
        }

        /// <summary>Validates API-boundary input and returns the stills sorted by capture time.</summary>
        private static IReadOnlyList<TimelineStill> ValidateAndOrder(TimelineVideoRequest request)
        {
            if (request.Stills is null || request.Stills.Count == 0)
            {
                throw new ArgumentException("At least one still is required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.OutputPath))
            {
                throw new ArgumentException("An output path is required.", nameof(request));
            }

            foreach (TimelineStill still in request.Stills)
            {
                if (string.IsNullOrWhiteSpace(still.ImagePath) || !File.Exists(still.ImagePath))
                {
                    throw new ArgumentException("Every still must reference an existing image file.", nameof(request));
                }

                if (still.Hold <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Every still must have a positive hold.", nameof(request));
                }
            }

            return request.Stills.OrderBy(s => s.CapturedAt).ToList();
        }

        /// <summary>
        /// Rounds a hold to whole seconds, with a minimum of one second. Half-seconds round away from zero,
        /// so 1.5 s becomes 2 s and 1.4 s becomes 1 s.
        /// </summary>
        private static int WholeSecondHold(TimeSpan hold)
        {
            int seconds = (int)Math.Round(hold.TotalSeconds, MidpointRounding.AwayFromZero);
            return Math.Max(1, seconds);
        }

        /// <summary>
        /// Reads each still's width and height with ffprobe (one run per file, since ffprobe takes a single input)
        /// and returns the bounding box, rounded up to even dimensions. Failures are returned, not thrown, except cancellation.
        /// </summary>
        private async Task<(bool Ok, int Width, int Height, string? Error)> TryProbeTargetSizeAsync(
            IReadOnlyList<TimelineStill> stills, CancellationToken ct)
        {
            int maxWidth = 0;
            int maxHeight = 0;
            foreach (TimelineStill still in stills)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                foreach (string arg in new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "csv=p=0" })
                {
                    startInfo.ArgumentList.Add(arg);
                }
                startInfo.ArgumentList.Add(Path.GetFullPath(still.ImagePath));

                ProcessResult run;
                try
                {
                    run = await ProcessRunner.RunAsync(startInfo, _timeout, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return (false, 0, 0, ex.Message);
                }

                if (run.ExitCode != 0)
                {
                    return (false, 0, 0, $"ffprobe exited with code {run.ExitCode}.");
                }

                // Output is "width,height" for the single input.
                string[] parts = run.StandardOutput.Trim().Split(',');
                if (parts.Length != 2
                    || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int w)
                    || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int h)
                    || w <= 0 || h <= 0)
                {
                    return (false, 0, 0, "ffprobe returned an unreadable image size.");
                }

                maxWidth = Math.Max(maxWidth, w);
                maxHeight = Math.Max(maxHeight, h);
            }

            return (true, RoundUpToEven(maxWidth), RoundUpToEven(maxHeight), null);
        }

        private static int RoundUpToEven(int value) => value % 2 == 0 ? value : value + 1;

        /// <summary>
        /// Builds the concat-demuxer list. Each still gets a file line and a duration line; the last still is
        /// listed once more without a duration so ffmpeg does not drop its final hold.
        /// </summary>
        private static string BuildConcatList(IReadOnlyList<TimelineStill> stills)
        {
            var sb = new StringBuilder();
            foreach (TimelineStill still in stills)
            {
                sb.Append("file '").Append(EscapeConcatPath(Path.GetFullPath(still.ImagePath))).Append("'\n");
                sb.Append("duration ").Append(WholeSecondHold(still.Hold).ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            sb.Append("file '").Append(EscapeConcatPath(Path.GetFullPath(stills[^1].ImagePath))).Append("'\n");
            return sb.ToString();
        }

        /// <summary>Escapes a single quote for the concat demuxer's single-quoted path syntax.</summary>
        private static string EscapeConcatPath(string path) => path.Replace("'", "'\\''", StringComparison.Ordinal);

        /// <summary>Derives the ffprobe path that sits beside an explicit ffmpeg path.</summary>
        private static string DeriveFfprobePath(string ffmpegPath)
        {
            string? directory = Path.GetDirectoryName(ffmpegPath);
            string fileName = Path.GetFileName(ffmpegPath);
            string probeName = fileName.Replace("ffmpeg", "ffprobe", StringComparison.OrdinalIgnoreCase);
            return string.IsNullOrEmpty(directory) ? probeName : Path.Combine(directory, probeName);
        }

        private void TryDeleteDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not delete timeline temp directory");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Could not delete timeline temp directory");
            }
        }
    }
}
