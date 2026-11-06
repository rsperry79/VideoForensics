using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>Response shape of the alarm account-monitoring-status call for a location.</summary>
    public class AccountMonitoringStatusResponse
    {
        [JsonPropertyName("addressComplete")]
        public bool? AddressComplete { get; set; }

        [JsonPropertyName("contactsComplete")]
        public bool? ContactsComplete { get; set; }

        [JsonPropertyName("codewordComplete")]
        public bool? CodewordComplete { get; set; }

        [JsonPropertyName("dispatchSetupComplete")]
        public bool? DispatchSetupComplete { get; set; }

        [JsonPropertyName("userChangedAddress")]
        public bool? UserChangedAddress { get; set; }

        [JsonPropertyName("alarmSignalSent")]
        public bool? AlarmSignalSent { get; set; }

        [JsonPropertyName("professionallyMonitored")]
        public bool? ProfessionallyMonitored { get; set; }

        [JsonPropertyName("userAcceptDispatch")]
        public bool? UserAcceptDispatch { get; set; }

        [JsonPropertyName("vrRequired")]
        public bool? VrRequired { get; set; }

        [JsonPropertyName("vrUserOptIn")]
        public bool? VrUserOptIn { get; set; }

        [JsonPropertyName("cmsMonitoringType")]
        public string? CmsMonitoringType { get; set; }

        [JsonPropertyName("suspectedBusiness")]
        public bool? SuspectedBusiness { get; set; }

        [JsonPropertyName("compatibleMonitoringPlans")]
        public List<string>? CompatibleMonitoringPlans { get; set; }

        [JsonPropertyName("accountStateReason")]
        public string? AccountStateReason { get; set; }

        [JsonPropertyName("accountUuid")]
        public string? AccountUuid { get; set; }

        [JsonPropertyName("accountState")]
        public string? AccountState { get; set; }

        [JsonPropertyName("externalServiceConfigType")]
        public string? ExternalServiceConfigType { get; set; }

        [JsonPropertyName("eligibleForDispatch")]
        public bool? EligibleForDispatch { get; set; }

        [JsonPropertyName("sosOnly")]
        public bool? SosOnly { get; set; }

        [JsonPropertyName("installationDate")]
        public long? InstallationDate { get; set; }

        [JsonPropertyName("deliverSignals")]
        public bool? DeliverSignals { get; set; }

        [JsonPropertyName("planProvider")]
        public string? PlanProvider { get; set; }

        [JsonPropertyName("sosOnlyFlagOrFalse")]
        public bool? SosOnlyFlagOrFalse { get; set; }
    }
}
