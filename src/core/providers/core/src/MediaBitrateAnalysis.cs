using System.Globalization;
using System.Text.Json;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Bitrate and duration of a downloaded media file, from ffprobe output.</summary>
    /// <param name="VideoBitrateBps">Video bitrate in bits per second.</param>
    /// <param name="DurationSeconds">Duration of the file in seconds.</param>
    /// <param name="BitrateEstimated">True when the stream reported no bitrate and it was derived from file size and duration.</param>
    public sealed record MediaBitrateAnalysis(long VideoBitrateBps, double DurationSeconds, bool BitrateEstimated)
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
        public static MediaBitrateAnalysis? FromProbe(TimeSpan duration, long? videoStreamBitrate, long fileSizeBytes)
        {
            if (duration <= TimeSpan.Zero)
            {
                return null;
            }

            double seconds = duration.TotalSeconds;
            if (videoStreamBitrate is > 0)
            {
                return new MediaBitrateAnalysis(videoStreamBitrate.Value, seconds, BitrateEstimated: false);
            }

            if (fileSizeBytes <= 0)
            {
                return null;
            }

            return new MediaBitrateAnalysis((long)Math.Round(fileSizeBytes * 8.0 / seconds), seconds, BitrateEstimated: true);
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
