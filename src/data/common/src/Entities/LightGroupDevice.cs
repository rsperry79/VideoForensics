namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One device id that belongs to a <see cref="LightGroup"/>.</summary>
    public class LightGroupDevice
    {
        public Guid Id { get; set; }
        public Guid LightGroupId { get; set; }
        public string ProviderDeviceId { get; set; } = string.Empty;
    }
}
