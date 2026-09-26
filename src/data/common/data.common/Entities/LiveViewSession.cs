namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Represents an active or completed live-view session for a camera device.</summary>
    public class LiveViewSession
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public LiveViewTriggerReason TriggerReason { get; set; }
        public LiveViewSessionState State { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
        public DateTime LastExtendedAtUtc { get; set; }
        public bool IsSustained { get; set; }
        public DateTime? SustainedSinceUtc { get; set; }
        public string? PromotionReason { get; set; }
        public Guid? OperatorId { get; set; }
        public string? StopReason { get; set; }
        public string? ProviderSessionRef { get; set; }
    }

    public enum LiveViewTriggerReason
    {
        Manual,
        JammingSuspected,
        JammingConfirmed,
        Calibration
    }

    public enum LiveViewSessionState
    {
        Starting,
        Active,
        Sustained,
        Stopping,
        Stopped,
        Failed
    }
}
