using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Classifies Ring provider API errors for logging and diagnostics.
    /// </summary>
    internal static class RingProviderApiErrorClassifier
    {
        /// <summary>
        /// Classifies a download failure for the ProviderApiErrorLog table. The interesting case is
        /// DeviceUnknownException (Ring 404): if this event previously downloaded successfully
        /// (existingRecord.Success == true), Ring no longer having it means the recording was deleted
        /// after the fact - a materially different situation from an event that was never available.
        /// </summary>
        internal static string ClassifyProviderApiError(Exception ex, DownloadEvent? existingRecord)
        {
            return ex switch
            {
                VideoForensics.Providers.Ring.Exceptions.ThrottledException => "RateLimited",
                VideoForensics.Providers.Ring.Exceptions.DeviceUnknownException =>
                    existingRecord?.Success == true ? "RecordingDeletedAfterDownload" : "RecordingNotFound",
                VideoForensics.Providers.Ring.Exceptions.DownloadFailedException => "DownloadFailed",
                VideoForensics.Providers.Ring.Exceptions.UnexpectedOutcomeException => "UnexpectedStatus",
                OperationCanceledException => "Cancelled",
                _ => "Other"
            };
        }

        /// <summary>Extracts the HTTP status code from whichever of the four Ring exception types carries one, or null.</summary>
        internal static int? GetHttpStatusCode(Exception ex)
        {
            return ex switch
            {
                VideoForensics.Providers.Ring.Exceptions.ThrottledException te => te.StatusCode.HasValue ? (int)te.StatusCode.Value : null,
                VideoForensics.Providers.Ring.Exceptions.DeviceUnknownException due => due.StatusCode.HasValue ? (int)due.StatusCode.Value : null,
                VideoForensics.Providers.Ring.Exceptions.DownloadFailedException dfe => dfe.StatusCode.HasValue ? (int)dfe.StatusCode.Value : null,
                VideoForensics.Providers.Ring.Exceptions.UnexpectedOutcomeException uoe => (int)uoe.ReturnedStatusCode,
                _ => null
            };
        }

        /// <summary>Extracts the (already-truncated) raw response body from whichever of the four Ring exception types carries one, or null.</summary>
        internal static string? GetResponseBody(Exception ex)
        {
            return ex switch
            {
                VideoForensics.Providers.Ring.Exceptions.ThrottledException te => te.ResponseBody,
                VideoForensics.Providers.Ring.Exceptions.DeviceUnknownException due => due.ResponseBody,
                VideoForensics.Providers.Ring.Exceptions.DownloadFailedException dfe => dfe.ResponseBody,
                VideoForensics.Providers.Ring.Exceptions.UnexpectedOutcomeException uoe => uoe.ResponseBody,
                _ => null
            };
        }
    }
}
