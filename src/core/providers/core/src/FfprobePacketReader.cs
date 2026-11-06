using System.Diagnostics;
using System.Globalization;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>
    /// Reads every video packet's start time and size from a file with ffprobe. Summing the sizes per second gives the
    /// bitrate over the whole clip, unlike the file-level average, which hides a drop in part of the clip.
    /// </summary>
    public static class FfprobePacketReader
    {
        /// <summary>Reads the video packets of a media file. Returns null when ffprobe cannot read the file.</summary>
        /// <param name="ffprobePath">Path to ffprobe, or a bare name found on PATH.</param>
        /// <param name="mediaFilePath">Absolute path of the media file.</param>
        /// <param name="ct">Cancellation token.</param>
        public static async Task<IReadOnlyList<(double PtsSeconds, long SizeBytes)>?> ReadVideoPacketsAsync(string ffprobePath, string mediaFilePath, CancellationToken ct)
        {
            var start = new ProcessStartInfo(FfmpegPathResolver.Resolve(ffprobePath, "ffprobe"))
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("-v");
            start.ArgumentList.Add("error");
            start.ArgumentList.Add("-select_streams");
            start.ArgumentList.Add("v:0");
            start.ArgumentList.Add("-show_entries");
            start.ArgumentList.Add("packet=pts_time,size");
            start.ArgumentList.Add("-of");
            start.ArgumentList.Add("csv=p=0");
            start.ArgumentList.Add(mediaFilePath);

            using Process process = Process.Start(start) ?? throw new InvalidOperationException("ffprobe did not start.");
            string output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0)
            {
                return null;
            }

            var packets = new List<(double, long)>();
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (ParsePacketLine(line) is { } packet)
                {
                    packets.Add(packet);
                }
            }

            return packets;
        }

        /// <summary>Parses one "pts_time,size" line. Returns null when either field is missing or not a number.</summary>
        public static (double PtsSeconds, long SizeBytes)? ParsePacketLine(string line)
        {
            string[] fields = line.Trim().Split(',');
            return fields.Length != 2
                || !double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double pts)
                || !long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long size)
                ? null
                : (pts, size);
        }
    }
}
