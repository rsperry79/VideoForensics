namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only location fields from GET devices/v1/locations (1:1 with the location). Typed columns named after the
    /// Ring key; null means Ring did not send the key. Ring's created_at and updated_at are stored as UTC here.
    /// </summary>
    public class RingLocationDetails
    {
        public Guid Id { get; set; }
        public Guid LocationId { get; set; }
        public DateTime? CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public long? OwnerId { get; set; }
        public bool? BusinessCommandCenter { get; set; }
        public bool? GuardPatrol { get; set; }
        public bool? PrivateGuardResponse { get; set; }
        public bool? VmVideoVerification { get; set; }
        public bool? VmVirtualSecurityGuard { get; set; }
        public bool? UserVerified { get; set; }
        public bool? IsJobsite { get; set; }
        public string? GeoServiceVerified { get; set; }
        public string? LocationType { get; set; }
        public string? LocationResourceId { get; set; }
    }
}
