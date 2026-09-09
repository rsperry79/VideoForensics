namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Ring account feature flags and profile information.</summary>
    public class RingAccountFeatures
    {
        public Guid Id { get; set; }
        public Guid RingAccountId { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? CanViewLiveView { get; set; }
        public bool? CanViewRecordings { get; set; }
        public bool? CanViewSnapshotsOnly { get; set; }
        public bool? CanSaveLiveView { get; set; }
        public bool? CanSaveRecordings { get; set; }
        public bool? CanDeleteRecordings { get; set; }
        public bool? CanPauseRecordings { get; set; }
        public bool? CanShareRecordings { get; set; }
        public bool? CanChangeDeviceSettings { get; set; }
        public bool? CanActivateAlarmSystem { get; set; }
    }
}
