namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Junction entity linking a forensic case to the devices in its scope.</summary>
    public class CaseDevice
    {
        /// <summary>Gets or sets the ID of the forensic case (composite key).</summary>
        public Guid CaseId { get; set; }

        /// <summary>Gets or sets the ID of the device in scope (composite key).</summary>
        public Guid DeviceId { get; set; }

        /// <summary>Gets or sets the UTC timestamp when this device was added to the case scope.</summary>
        public DateTime AddedAtUtc { get; set; }
    }
}
