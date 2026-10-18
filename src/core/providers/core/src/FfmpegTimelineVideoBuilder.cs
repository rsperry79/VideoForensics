using System.Diagnostics;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>
    /// Builds a 1 fps H.264 MP4 from timed still images and optional event clips.
    /// The timeline is planned into ordered segments, each encoded to its own temp MP4 with exactly its whole-second
    /// frame count, then joined with the concat demuxer and stream copy. Every segment is scaled and centre-padded to
    /// the bounding box of all inputs (largest width by largest height), rounded up to even dimensions because
    /// libx264 with yuv420p rejects odd sizes.
    /// <para>
    /// Segment rules, all rounding by <c>WholeSecondHold</c> (away from zero, minimum 1 s):
    /// <list type="bullet">
    /// <item>An uncut still shows for its hold, rounded.</item>
    /// <item>A clip replaces the stills inside its window, so a still is cut around clip windows. A cut-off remainder
    /// under 1 s before rounding is dropped; a remainder of 1 s or more is rounded like a hold.</item>
    /// <item>A clip window is rounded like a hold, so a sub-second window still gets 1 frame.</item>
    /// <item>A clip's usable seconds are capped at the real video left after its offset: the smaller of the window
    /// seconds and the rounded real duration minus the offset. The encoded frame count therefore matches the video
    /// actually read. A clip with no real video left after its offset contributes no segment.</item>
    /// </list>
    /// Overlapping clips resolve to the one that starts first; the later clip is trimmed to start where the earlier
    /// one ends. Declared windows are used for placement, so a part of a declared window past the clip's real end is a
    /// gap: it still cuts stills, but nothing fills it. Gaps between segments are not filled.
    /// </para>
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
            IReadOnlyList<TimelineClip> clips = ValidateClips(request);
            string outputPath = Path.GetFullPath(request.OutputPath);

            ct.ThrowIfCancellationRequested();

            (bool durationsOk, IReadOnlyList<ProbedClip> probedClips, string? durationError) = await TryProbeClipDurationsAsync(clips, ct);
            if (!durationsOk)
            {
                _logger.LogError("Timeline video build could not read clip durations for {ClipCount} clips: {Error}", clips.Count, durationError);
                return new TimelineVideoResult(false, null, durationError, 0);
            }

            var mediaPaths = stills.Select(s => s.ImagePath).Concat(clips.Select(c => c.VideoPath)).ToList();
            (bool sized, int width, int height, string? sizeError) = await TryProbeTargetSizeAsync(mediaPaths, ct);
            if (!sized)
            {
                _logger.LogError("Timeline video build could not read media dimensions for {MediaCount} inputs: {Error}", mediaPaths.Count, sizeError);
                return new TimelineVideoResult(false, null, sizeError, 0);
            }

            IReadOnlyList<Segment> segments = PlanSegments(stills, probedClips);
            if (segments.Count == 0)
            {
                _logger.LogError("Timeline video build has no segments left after applying {ClipCount} clips", clips.Count);
                return new TimelineVideoResult(false, null, "No timeline segments remain after applying the clips.", 0);
            }

            int frameCount = segments.Sum(s => s.Seconds);

            string workDir = Path.Combine(Path.GetTempPath(), "timeline-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(workDir);

                string? outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                string filter =
                    $"fps=1,scale={width}:{height}:force_original_aspect_ratio=decrease," +
                    $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2";

                var segmentPaths = new List<string>(segments.Count);
                for (int i = 0; i < segments.Count; i++)
                {
                    string segmentPath = Path.Combine(workDir, $"segment-{i:D4}.mp4");
                    string? encodeError = await TryRunFfmpegAsync(BuildSegmentArgs(segments[i], filter, segmentPath), ct);
                    if (encodeError is not null)
                    {
                        return new TimelineVideoResult(false, null, encodeError, 0);
                    }

                    segmentPaths.Add(segmentPath);
                }

                string listPath = Path.Combine(workDir, "segments.txt");
                await File.WriteAllTextAsync(listPath, BuildConcatList(segmentPaths), ct);

                string? joinError = await TryRunFfmpegAsync(BuildConcatArgs(listPath, outputPath), ct);
                if (joinError is not null || !File.Exists(outputPath))
                {
                    return new TimelineVideoResult(false, null, joinError ?? "ffmpeg did not write the output file.", 0);
                }

                _logger.LogInformation(
                    "Built timeline video from {StillCount} stills and {ClipCount} clips: {FrameCount} frames in {SegmentCount} segments at {Width}x{Height}",
                    stills.Count, clips.Count, frameCount, segments.Count, width, height);
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

        /// <summary>Validates the optional clip list. Clip video streams are checked later by the size probe.</summary>
        private static IReadOnlyList<TimelineClip> ValidateClips(TimelineVideoRequest request)
        {
            if (request.Clips is null)
            {
                return Array.Empty<TimelineClip>();
            }

            foreach (TimelineClip clip in request.Clips)
            {
                if (string.IsNullOrWhiteSpace(clip.VideoPath) || !File.Exists(clip.VideoPath))
                {
                    throw new ArgumentException("Every clip must reference an existing video file.", nameof(request));
                }

                if (clip.Duration <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Every clip must have a positive duration.", nameof(request));
                }
            }

            return request.Clips;
        }

        /// <summary>
        /// Turns stills and clips into the ordered list of segments to encode. Clip windows are resolved first (earlier
        /// start wins, later clip trimmed), then each still is cut around them. Windows with no usable seconds still cut
        /// stills but produce no segment, which leaves that span as a gap.
        /// </summary>
        private static IReadOnlyList<Segment> PlanSegments(IReadOnlyList<TimelineStill> stills, IReadOnlyList<ProbedClip> clips)
        {
            List<ClipWindow> windows = ResolveClipWindows(clips);
            var items = new List<(DateTimeOffset Start, Segment Segment)>();

            foreach (TimelineStill still in stills)
            {
                DateTimeOffset stillEnd = still.CapturedAt + still.Hold;
                List<ClipWindow> overlapping = windows
                    .Where(w => w.End > still.CapturedAt && w.Start < stillEnd)
                    .ToList();

                if (overlapping.Count == 0)
                {
                    // An uncut still keeps the original rule: its hold rounded to whole seconds, minimum 1 s.
                    items.Add((still.CapturedAt, new Segment(false, still.ImagePath, TimeSpan.Zero, WholeSecondHold(still.Hold))));
                    continue;
                }

                foreach ((DateTimeOffset pieceStart, TimeSpan length) in SubtractClips(still.CapturedAt, stillEnd, overlapping))
                {
                    // A cut-off remainder under 1 s before rounding is dropped; 1 s or more is rounded like a hold.
                    if (length < TimeSpan.FromSeconds(1))
                    {
                        continue;
                    }

                    items.Add((pieceStart, new Segment(false, still.ImagePath, TimeSpan.Zero, WholeSecondHold(length))));
                }
            }

            foreach (ClipWindow window in windows)
            {
                if (window.Seconds > 0)
                {
                    items.Add((window.Start, new Segment(true, window.VideoPath, window.Offset, window.Seconds)));
                }
            }

            // OrderBy is stable, so equal start times keep their insertion order.
            return items.OrderBy(i => i.Start).Select(i => i.Segment).ToList();
        }

        /// <summary>
        /// Resolves clip windows so they do not overlap: clips are taken in start order, and a clip that starts before
        /// the previous accepted clip ends is trimmed to start where that clip ends. A clip fully covered by an earlier
        /// one is dropped. Returned windows are sorted and disjoint. Placement uses the declared window; the usable
        /// seconds are capped at the real video left after the window's offset (see <see cref="ClipWindow.Seconds"/>).
        /// </summary>
        private static List<ClipWindow> ResolveClipWindows(IReadOnlyList<ProbedClip> clips)
        {
            var windows = new List<ClipWindow>();
            DateTimeOffset cursor = DateTimeOffset.MinValue;
            bool hasCursor = false;

            foreach (ProbedClip probed in clips.OrderBy(c => c.Clip.StartedAt))
            {
                TimelineClip clip = probed.Clip;
                DateTimeOffset end = clip.StartedAt + clip.Duration;
                DateTimeOffset start = hasCursor && cursor > clip.StartedAt ? cursor : clip.StartedAt;
                if (end <= start)
                {
                    continue;
                }

                TimeSpan offset = start - clip.StartedAt;
                // The declared window is rounded like a hold. Real video is only read up to its end, so a window
                // past the real end gets fewer seconds, or none when no real video is left after the offset.
                int declaredSeconds = WholeSecondHold(end - start);
                double realSecondsLeft = probed.RealSeconds - offset.TotalSeconds;
                int seconds = realSecondsLeft > 0
                    ? Math.Min(declaredSeconds, WholeSecondHold(TimeSpan.FromSeconds(realSecondsLeft)))
                    : 0;

                // The window keeps its declared span even when it yields no seconds, so it still cuts stills.
                windows.Add(new ClipWindow(clip.VideoPath, start, offset, seconds, end));
                cursor = end;
                hasCursor = true;
            }

            return windows;
        }

        /// <summary>
        /// Returns the parts of [start, end) that no window covers, with their lengths. Windows must be sorted and disjoint.
        /// </summary>
        private static List<(DateTimeOffset Start, TimeSpan Length)> SubtractClips(
            DateTimeOffset start, DateTimeOffset end, IReadOnlyList<ClipWindow> windows)
        {
            var pieces = new List<(DateTimeOffset Start, TimeSpan Length)>();
            DateTimeOffset cursor = start;
            foreach (ClipWindow window in windows)
            {
                if (window.End <= cursor || window.Start >= end)
                {
                    continue;
                }

                if (window.Start > cursor)
                {
                    pieces.Add((cursor, window.Start - cursor));
                }

                cursor = window.End;
            }

            if (cursor < end)
            {
                pieces.Add((cursor, end - cursor));
            }

            return pieces;
        }

        /// <summary>
        /// The one rounding rule for stills, cut remainders and clip windows: round to whole seconds with a minimum of
        /// one second. Half-seconds round away from zero, so 1.5 s becomes 2 s, 1.4 s becomes 1 s, and 0.4 s becomes 1 s.
        /// </summary>
        private static int WholeSecondHold(TimeSpan hold)
        {
            int seconds = (int)Math.Round(hold.TotalSeconds, MidpointRounding.AwayFromZero);
            return Math.Max(1, seconds);
        }

        /// <summary>
        /// Reads each clip's real duration with ffprobe (one run per clip) so segments never ask for more video than
        /// the file holds. Failures are returned, not thrown, except cancellation; each message names the clip.
        /// </summary>
        private async Task<(bool Ok, IReadOnlyList<ProbedClip> Clips, string? Error)> TryProbeClipDurationsAsync(
            IReadOnlyList<TimelineClip> clips, CancellationToken ct)
        {
            var probed = new List<ProbedClip>(clips.Count);
            foreach (TimelineClip clip in clips)
            {
                string name = Path.GetFileName(clip.VideoPath);
                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                foreach (string arg in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0" })
                {
                    startInfo.ArgumentList.Add(arg);
                }
                startInfo.ArgumentList.Add(Path.GetFullPath(clip.VideoPath));

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
                    return (false, Array.Empty<ProbedClip>(), $"Could not read the duration of clip '{name}': {ex.Message}");
                }

                if (run.ExitCode != 0)
                {
                    return (false, Array.Empty<ProbedClip>(), $"ffprobe could not read the duration of clip '{name}' (exit code {run.ExitCode}).");
                }

                // Output is the duration in decimal seconds, e.g. "2.000000".
                if (!double.TryParse(run.StandardOutput.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                    || !double.IsFinite(seconds)
                    || seconds <= 0)
                {
                    return (false, Array.Empty<ProbedClip>(), $"ffprobe returned an unreadable duration for clip '{name}'.");
                }

                probed.Add(new ProbedClip(clip, seconds));
            }

            return (true, probed, null);
        }

        /// <summary>
        /// Reads each media file's width and height with ffprobe (one run per file, since ffprobe takes a single input)
        /// and returns the bounding box, rounded up to even dimensions. A clip without a video stream fails here.
        /// Failures are returned, not thrown, except cancellation.
        /// </summary>
        private async Task<(bool Ok, int Width, int Height, string? Error)> TryProbeTargetSizeAsync(
            IReadOnlyList<string> mediaPaths, CancellationToken ct)
        {
            int maxWidth = 0;
            int maxHeight = 0;
            foreach (string mediaPath in mediaPaths)
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
                startInfo.ArgumentList.Add(Path.GetFullPath(mediaPath));

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
        /// Builds the ffmpeg arguments that encode one segment to a 1 fps MP4 of exactly its whole-second frame count.
        /// A still loops for its duration; a clip is seeked to the offset of its used window and read for its duration.
        /// </summary>
        private static IReadOnlyList<string> BuildSegmentArgs(Segment segment, string filter, string segmentPath)
        {
            string seconds = segment.Seconds.ToString(CultureInfo.InvariantCulture);
            var args = new List<string>();
            if (segment.IsClip)
            {
                string offset = segment.Offset.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
                args.AddRange(new[] { "-ss", offset, "-t", seconds, "-i", segment.SourcePath });
            }
            else
            {
                args.AddRange(new[] { "-loop", "1", "-i", segment.SourcePath, "-t", seconds });
            }

            args.AddRange(new[]
            {
                "-vf", filter,
                "-c:v", "libx264", "-pix_fmt", "yuv420p",
                "-fps_mode", "cfr", "-g", "1", "-an",
                "-y", segmentPath
            });
            return args;
        }

        /// <summary>
        /// Builds the ffmpeg arguments that join the encoded segments without re-encoding. The segments share one
        /// encoder setup, so stream copy is valid.
        /// </summary>
        private static IReadOnlyList<string> BuildConcatArgs(string listPath, string outputPath)
        {
            return new[] { "-f", "concat", "-safe", "0", "-i", listPath, "-c", "copy", "-y", outputPath };
        }

        /// <summary>Runs ffmpeg with the given arguments. Returns null on success, or the failure reason.</summary>
        private async Task<string?> TryRunFfmpegAsync(IReadOnlyList<string> args, CancellationToken ct)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string arg in args)
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
                _logger.LogError(ex, "Timeline video build could not run ffmpeg");
                return ex.Message;
            }

            if (run.ExitCode != 0)
            {
                _logger.LogError("Timeline video build ffmpeg step failed, exit code {Code}: {Stderr}", run.ExitCode, run.StandardError);
                return $"ffmpeg exited with code {run.ExitCode}.";
            }

            return null;
        }

        /// <summary>
        /// Builds the concat-demuxer list over the encoded segment files. Each segment is listed once, in timeline order.
        /// </summary>
        private static string BuildConcatList(IReadOnlyList<string> segmentPaths)
        {
            var sb = new StringBuilder();
            foreach (string path in segmentPaths)
            {
                sb.Append("file '").Append(EscapeConcatPath(Path.GetFullPath(path))).Append("'\n");
            }

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
                _logger.LogError(ex, "Could not delete timeline temp directory");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Could not delete timeline temp directory");
            }
        }

        /// <summary>One piece of the output timeline: a still frame run or a clip run, with its whole-second frame count.</summary>
        /// <param name="IsClip">True for a clip segment, false for a still segment.</param>
        /// <param name="SourcePath">The still image or clip video file.</param>
        /// <param name="Offset">For a clip, how far into the clip the used window starts.</param>
        /// <param name="Seconds">Whole-second frame count of the segment.</param>
        private sealed record Segment(bool IsClip, string SourcePath, TimeSpan Offset, int Seconds);

        /// <summary>A clip with its ffprobe-measured real duration in seconds.</summary>
        private sealed record ProbedClip(TimelineClip Clip, double RealSeconds);

        /// <summary>A resolved, non-overlapping clip window on the timeline: [Start, End).</summary>
        /// <param name="VideoPath">The clip video file.</param>
        /// <param name="Start">Timeline start of the window, after any trimming.</param>
        /// <param name="Offset">How far into the clip the window starts (Start minus the clip's StartedAt).</param>
        /// <param name="Seconds">
        /// Usable whole-second frame count: the window's rounded seconds, capped at the rounded real video left after
        /// <paramref name="Offset"/>. Zero means no real video is left, so the window cuts stills but adds no segment.
        /// </param>
        /// <param name="End">Timeline end of the declared window.</param>
        private sealed record ClipWindow(string VideoPath, DateTimeOffset Start, TimeSpan Offset, int Seconds, DateTimeOffset End);
    }
}
