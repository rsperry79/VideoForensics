namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A download attempt record for an event from a device.</summary>
    public class DownloadEvent
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public required string ProviderEventId { get; set; }
        public string? EventType { get; set; }
        public bool Answered { get; set; }
        public bool Favorite { get; set; }
        public DateTime EventOccurredAtUtc { get; set; }
        public string? RecordingStatus { get; set; }
        public DateTime DownloadStartedUtc { get; set; }
        public DateTime? DownloadCompletedUtc { get; set; }
        public bool Success { get; set; }
        public int AttemptCount { get; set; }
        public string? ErrorMessage { get; set; }
        public required string AppVersion { get; set; }
    }
}
