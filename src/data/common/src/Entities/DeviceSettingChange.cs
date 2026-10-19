namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// One recorded change to a scalar device setting. Written only when a stored value actually changes during a
    /// device sync; first-time population of a previously null column is not recorded.
    /// </summary>
    public class DeviceSettingChange
    {
        /// <summary>Gets or sets the unique identifier of this change record.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the device whose setting changed.</summary>
        public Guid DeviceId { get; set; }

        /// <summary>Gets or sets the name of the Device property that changed (at most 64 characters).</summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the previously stored value as invariant-culture text, or null when the stored value was null.
        /// </summary>
        public string? OldValue { get; set; }

        /// <summary>Gets or sets the incoming value as invariant-culture text, or null when the incoming value is null.</summary>
        public string? NewValue { get; set; }

        /// <summary>Gets or sets the UTC time at which the change was observed.</summary>
        public DateTime ChangedAtUtc { get; set; }
    }
}
