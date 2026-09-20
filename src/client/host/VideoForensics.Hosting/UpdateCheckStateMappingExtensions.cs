using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>Extension methods for mapping update-check DTOs to/from domain types.</summary>
    public static class UpdateCheckStateMappingExtensions
    {
        /// <summary>
        /// Converts a domain UpdateCheckState to an UpdateCheckStateDto.
        /// </summary>
        public static UpdateCheckStateDto ToDto(this UpdateCheckState state)
        {
            return new UpdateCheckStateDto(
                UpdateAvailable: state.UpdateAvailable,
                LatestVersion: state.LatestVersion,
                CurrentVersion: state.CurrentVersion,
                DownloadUrl: state.DownloadUrl,
                LastCheckedUtc: state.LastCheckedUtc,
                ErrorMessage: state.ErrorMessage);
        }

        /// <summary>
        /// Converts an UpdateCheckStateDto to a domain UpdateCheckState.
        /// </summary>
        public static UpdateCheckState ToDomain(this UpdateCheckStateDto dto)
        {
            return new UpdateCheckState(
                UpdateAvailable: dto.UpdateAvailable,
                LatestVersion: dto.LatestVersion,
                CurrentVersion: dto.CurrentVersion,
                DownloadUrl: dto.DownloadUrl,
                LastCheckedUtc: dto.LastCheckedUtc,
                ErrorMessage: dto.ErrorMessage);
        }
    }
}
