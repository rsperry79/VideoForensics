using System.Globalization;
using System.Text.Json;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Bitrate and duration of a downloaded media file, from ffprobe output.</summary>
    /// <param name="VideoBitrateBps">Video bitrate in bits per second.</param>
    /// <param name="DurationSeconds">Duration of the file in seconds.</param>
    /// <param name="BitrateEstimated">True when the stream reported no bitrate and it was derived from file size and duration.</param>
    /// <param name="VideoCodec">Video codec name from the primary video stream, if known.</param>
    /// <param name="AudioCodec">Audio codec name from the primary audio stream, if known.</param>
    /// <param name="Resolution">Frame size as "WIDTHxHEIGHT", if known.</param>
    /// <param name="FrameRate">Average frame rate in frames per second, rounded to two places, if known.</param>
    public sealed record MediaBitrateAnalysis(
        long VideoBitrateBps,
        double DurationSeconds,
        bool BitrateEstimated,
        string? VideoCodec = null,
        string? AudioCodec = null,
        string? Resolution = null,
        decimal? FrameRate = null)
    {
        /// <summary>
        /// Parses <c>ffprobe -show_format -show_streams -of json</c> output. Prefers the video stream's own bitrate;
        /// Ring's MP4 files often omit it, so the average is derived from the file size and duration instead.
        /// Returns null when the output is not usable (not JSON, no duration).
        /// </summary>
        public static MediaBitrateAnalysis? FromFfprobeJson(string json, long fileSizeBytes)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                double? duration = ReadNumber(root, "format", "duration");
                if (duration is not > 0)
                {
                    return null;
                }

                long? streamBitrate = null;
                if (root.TryGetProperty("streams", out JsonElement streams) && streams.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement stream in streams.EnumerateArray())
                    {
                        if (stream.TryGetProperty("codec_type", out JsonElement codecType)
                            && codecType.GetString() == "video"
                            && ReadNumber(stream, "bit_rate") is { } bitrate and > 0)
                        {
                            streamBitrate = (long)bitrate;
                            break;
                        }
                    }
                }

                if (streamBitrate is { } reported)
                {
                    return new MediaBitrateAnalysis(reported, duration.Value, BitrateEstimated: false);
                }

                if (fileSizeBytes <= 0)
                {
                    return null;
                }

                long estimated = (long)Math.Round(fileSizeBytes * 8.0 / duration.Value);
                return new MediaBitrateAnalysis(estimated, duration.Value, BitrateEstimated: true);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Builds the analysis from values already read by FFMpegCore. Same rules as <see cref="FromFfprobeJson"/>: the video
        /// stream bitrate wins; otherwise the average is estimated from file size and duration. Null when there is no duration.
        /// </summary>
        public static MediaBitrateAnalysis? FromProbe(
            TimeSpan duration,
            long? videoStreamBitrate,
            long fileSizeBytes,
            string? videoCodec = null,
            string? audioCodec = null,
            int? width = null,
            int? height = null,
            double? frameRate = null)
        {
            MediaBitrateAnalysis? analysis = FromProbeBitrate(duration, videoStreamBitrate, fileSizeBytes);
            return analysis is null
                ? null
                : (analysis with
                {
                    VideoCodec = videoCodec,
                    AudioCodec = audioCodec,
                    Resolution = width is > 0 && height is > 0 ? $"{width}x{height}" : null,
                    FrameRate = frameRate is > 0 ? Math.Round((decimal)frameRate.Value, 2) : null
                });
        }

        private static MediaBitrateAnalysis? FromProbeBitrate(TimeSpan duration, long? videoStreamBitrate, long fileSizeBytes)
        {
            if (duration <= TimeSpan.Zero)
            {
                return null;
            }

            double seconds = duration.TotalSeconds;
            return videoStreamBitrate is > 0
                ? new MediaBitrateAnalysis(videoStreamBitrate.Value, seconds, BitrateEstimated: false)
                : fileSizeBytes <= 0
                ? null
                : new MediaBitrateAnalysis((long)Math.Round(fileSizeBytes * 8.0 / seconds), seconds, BitrateEstimated: true);
        }

        /// <summary>ffprobe writes numbers as strings, so accept either form.</summary>
        private static double? ReadNumber(JsonElement element, params string[] path)
        {
            JsonElement current = element;
            foreach (string segment in path)
            {
                if (!current.TryGetProperty(segment, out current))
                {
                    return null;
                }
            }

            return current.ValueKind switch
            {
                JsonValueKind.Number => current.GetDouble(),
                JsonValueKind.String when double.TryParse(current.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) => value,
                _ => null
            };
        }
    }
}
