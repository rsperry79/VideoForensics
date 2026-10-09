using VideoForensics.Api.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>Extension methods for mapping download-status DTOs to/from the Providers.Common domain type.</summary>
    public static class DownloadStatusDtoMapping
    {
        /// <summary>Converts a domain DownloadStatus to its wire DTO.</summary>
        public static DownloadStatusDto ToDto(this DownloadStatus status)
        {
            return new DownloadStatusDto(
                status.IsDownloading,
                status.FilesCompleted,
                status.FilesTotal,
                status.BytesDownloaded,
                status.CurrentFile,
                status.TotalFilesCompleted,
                status.TotalFilesMatched,
                status.TotalBytesDownloaded,
                status.ActiveConnections,
                status.CurrentSpeedMbps);
        }

        /// <summary>Converts a wire DownloadStatusDto back to the domain DownloadStatus.</summary>
        public static DownloadStatus ToDomain(this DownloadStatusDto dto)
        {
            return new DownloadStatus(
                dto.IsDownloading,
                dto.FilesCompleted,
                dto.FilesTotal,
                dto.BytesDownloaded,
                dto.CurrentFile,
                dto.TotalFilesCompleted,
                dto.TotalFilesMatched,
                dto.TotalBytesDownloaded,
                dto.ActiveConnections,
                dto.CurrentSpeedMbps);
        }
    }
}
