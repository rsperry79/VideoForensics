using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Reads video packets with ffprobe through <see cref="FfprobePacketReader"/>.</summary>
    public sealed class FfprobeMediaPacketReader(string? ffprobePath = null) : IMediaPacketReader
    {
        /// <inheritdoc />
        public Task<IReadOnlyList<(double PtsSeconds, long SizeBytes)>?> ReadVideoPacketsAsync(string absolutePath, CancellationToken ct)
        {
            return FfprobePacketReader.ReadVideoPacketsAsync(ffprobePath ?? "ffprobe", absolutePath, ct);
        }
    }
}
