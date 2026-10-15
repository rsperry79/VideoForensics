using System.Diagnostics;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Runs ffprobe on a downloaded file and returns its bitrate analysis.</summary>
    public sealed class FfprobeMediaBitrateAnalyzer
    {
        private readonly string _ffprobePath;

        /// <summary>Creates the analyzer. The ffprobe path is resolved like the other ffmpeg tools.</summary>
        public FfprobeMediaBitrateAnalyzer(string? ffprobePath = null)
        {
            _ffprobePath = FfmpegPathResolver.Resolve(ffprobePath, "ffprobe");
        }

        /// <summary>Returns the analysis, or null if ffprobe fails or the output is not usable.</summary>
        public async Task<MediaBitrateAnalysis?> AnalyzeAsync(string mediaFilePath, long fileSizeBytes, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("quiet");
            psi.ArgumentList.Add("-print_format");
            psi.ArgumentList.Add("json");
            psi.ArgumentList.Add("-show_format");
            psi.ArgumentList.Add("-show_streams");
            psi.ArgumentList.Add(mediaFilePath);

            using Process? process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            string output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0 ? MediaBitrateAnalysis.FromFfprobeJson(output, fileSizeBytes) : null;
        }
    }
}
