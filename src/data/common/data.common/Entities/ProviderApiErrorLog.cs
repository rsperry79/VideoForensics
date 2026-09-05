namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// A single failed provider API call's status code and (truncated) response body, tied to the
    /// Event it was attempting to download. Many rows can exist per Event (one per failed attempt),
    /// unlike DownloadEvent which is upserted and only ever holds the latest attempt's outcome.
    /// </summary>
    public class ProviderApiErrorLog
    {
        public Guid Id { get; set; }

        /// <summary>The Event.Id this failure relates to, when known (video downloads). Null for snapshot downloads, which have no Event row.</summary>
        public Guid? EventId { get; set; }

        public Guid DeviceId { get; set; }
        public int AttemptNumber { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? HttpMethod { get; set; }
        public string? RequestUrl { get; set; }
        public int? HttpStatusCode { get; set; }

        /// <summary>Truncated to ~4000 chars at write time.</summary>
        public string? ResponseBody { get; set; }

        public string? ExceptionType { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>One of: RateLimited, RecordingNotFound, RecordingDeletedAfterDownload, DownloadFailed, UnexpectedStatus, Cancelled, Other.</summary>
        public required string ErrorCategory { get; set; }
    }
}
