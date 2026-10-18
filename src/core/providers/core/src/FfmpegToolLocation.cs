using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Default in-memory implementation of <see cref="IFfmpegToolLocation"/>.</summary>
    public class FfmpegToolLocation : IFfmpegToolLocation
    {
        /// <inheritdoc />
        public string? BinDirectory { get; set; }
    }
}
