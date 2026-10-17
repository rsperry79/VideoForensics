namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// The 35 session feature flags of a Ring profile that have a dedicated column. Every other flag
    /// is stored in <see cref="RingAccountProfileFlag"/>. A null column means Ring did not send that flag.
    /// </summary>
    public class RingAccountSessionFeatures
    {
        public Guid Id { get; set; }
        public Guid RingAccountProfileId { get; set; }
        public bool? RemoteLoggingFormatStoring { get; set; }
        public int? RemoteLoggingLevel { get; set; }
        public bool? SubscriptionsEnabled { get; set; }
        public bool? StickupCamSetupEnabled { get; set; }
        public bool? VodEnabled { get; set; }
        public bool? NwEnabled { get; set; }
        public bool? NwUserActivated { get; set; }
        public bool? RingPlusEnabled { get; set; }
        public bool? LpdEnabled { get; set; }
        public bool? ReactiveSnoozingEnabled { get; set; }
        public bool? ProActiveSnoozingEnabled { get; set; }
        public bool? OwnerProActiveSnoozingEnabled { get; set; }
        public bool? LiveviewSettingsEnabled { get; set; }
        public bool? DeleteAllSettingsEnabled { get; set; }
        public bool? PowerCableEnabled { get; set; }
        public bool? DeviceHealthAlertsEnabled { get; set; }
        public bool? ChimeProEnabled { get; set; }
        public bool? MultipleCallsEnabled { get; set; }
        public bool? UjetEnabled { get; set; }
        public bool? MultipleDeleteEnabled { get; set; }
        public bool? DeleteAllEnabled { get; set; }
        public bool? LdpMotionAnnouncementEnabled { get; set; }
        public bool? StarredEventsEnabled { get; set; }
        public bool? ChimeDndEnabled { get; set; }
        public bool? VideoSearchEnabled { get; set; }
        public bool? FloodlightCamEnabled { get; set; }
        public bool? NwLargerAreaEnabled { get; set; }
        public bool? RingCamBatteryEnabled { get; set; }
        public bool? EliteCamEnabled { get; set; }
        public bool? DoorbellV2Enabled { get; set; }
        public bool? SpotlightBatteryDashboardControlsEnabled { get; set; }
        public bool? BypassAccountVerification { get; set; }
        public bool? LegacyCvrRetentionEnabled { get; set; }
        public bool? NewDashboardEnabled { get; set; }
        public bool? RingCamEnabled { get; set; }
    }
}
