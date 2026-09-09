namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Device geo/address metadata normalized from provider responses (P1 entity for provider metadata normalization).</summary>
    public class DeviceLocation
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Address { get; set; }
    }
}
