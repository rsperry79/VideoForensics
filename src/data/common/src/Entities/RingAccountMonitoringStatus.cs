namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Alarm account monitoring status for a Ring location; one row per location.</summary>
    public class RingAccountMonitoringStatus
    {
        public Guid Id { get; set; }
        public string ProviderLocationId { get; set; } = string.Empty;
        public bool? AddressComplete { get; set; }
        public bool? ContactsComplete { get; set; }
        public bool? CodewordComplete { get; set; }
        public bool? DispatchSetupComplete { get; set; }
        public bool? UserChangedAddress { get; set; }
        public bool? AlarmSignalSent { get; set; }
        public bool? ProfessionallyMonitored { get; set; }
        public bool? UserAcceptDispatch { get; set; }
        public bool? VrRequired { get; set; }
        public bool? VrUserOptIn { get; set; }
        public string? CmsMonitoringType { get; set; }
        public bool? SuspectedBusiness { get; set; }
        public string? AccountStateReason { get; set; }
        public string? AccountUuid { get; set; }
        public string? AccountState { get; set; }
        public string? ExternalServiceConfigType { get; set; }
        public bool? EligibleForDispatch { get; set; }
        public bool? SosOnly { get; set; }
        public long? InstallationDate { get; set; }
        public bool? DeliverSignals { get; set; }
        public string? PlanProvider { get; set; }
        public bool? SosOnlyFlagOrFalse { get; set; }
        public List<RingAccountMonitoringPlan> CompatibleMonitoringPlans { get; set; } = [];
    }
}
