namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One entry of a feature's ineligibility_reasons, disallow_reasons or transformer_score.affected_features array.</summary>
    public class RingDeviceFeatureReason
    {
        public const string Ineligibility = "ineligibility";
        public const string Disallow = "disallow";
        public const string AffectedFeature = "affected_feature";

        public Guid Id { get; set; }
        public Guid FeatureStateId { get; set; }

        /// <summary>"ineligibility", "disallow" or "affected_feature".</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>Index within the source array.</summary>
        public int Position { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
