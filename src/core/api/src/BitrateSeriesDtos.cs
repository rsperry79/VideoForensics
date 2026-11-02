using VideoForensics.Core.Providers.Core;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Wire DTO for one second of a clip's video bitrate.</summary>
    /// <param name="Second">Whole seconds from the start of the clip.</param>
    /// <param name="Kbps">Video bits in that second, in kilobits per second.</param>
    public sealed record BitrateSecondDto(int Second, long Kbps);

    /// <summary>Maps the domain bitrate point to its wire DTO.</summary>
    public static class BitrateSecondMapping
    {
        /// <summary>Converts a domain point to its wire DTO.</summary>
        public static BitrateSecondDto ToDto(this BitrateSecond point)
        {
            return new BitrateSecondDto(point.Second, point.Kbps);
        }
    }
}
