namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Singleton holder for the configured ffmpeg bin directory (IForensicsConfiguration.FfmpegBinDirectory).
    /// Consumers that resolve ffmpeg/ffprobe read <see cref="BinDirectory"/>; the configuration service writes it on load and save.
    /// </summary>
    public interface IFfmpegToolLocation
    {
        /// <summary>Directory containing ffmpeg and ffprobe, or null when unset (falls back to PATH).</summary>
        string? BinDirectory { get; set; }
    }
}
