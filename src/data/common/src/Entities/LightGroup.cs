namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One Smart Lighting group within a <see cref="LightGroupSet"/>.</summary>
    public class LightGroup
    {
        public Guid Id { get; set; }
        public Guid LightGroupSetId { get; set; }
        public string? ProviderDeviceGroupId { get; set; }
        public string? Name { get; set; }
        public List<LightGroupDevice> Devices { get; set; } = [];
    }
}
