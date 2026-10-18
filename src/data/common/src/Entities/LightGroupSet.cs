namespace VideoForensics.Data.Common.Entities
{
    /// <summary>The Smart Lighting groups response for one Ring location.</summary>
    public class LightGroupSet
    {
        public Guid Id { get; set; }
        /// <summary>Ring location id the groups endpoint is scoped by.</summary>
        public string ProviderLocationId { get; set; } = string.Empty;
        public bool? IsOwner { get; set; }
        public List<LightGroup> Groups { get; set; } = [];
    }
}
