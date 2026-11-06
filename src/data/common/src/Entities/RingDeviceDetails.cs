namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only top-level scalars of a ring_devices entry, one row per device, replaced on every sync. Typed columns named after the
    /// Ring key; null means Ring did not send it. Types of keys that were null in every captured sample are best guesses.
    /// </summary>
    public class RingDeviceDetails
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>Ring schema_id (a hash-like string).</summary>
        public string? SchemaId { get; set; }

        /// <summary>Ring is_sidewalk_gateway.</summary>
        public bool? IsSidewalkGateway { get; set; }

        /// <summary>Ring device_resource_id (an rduri string).</summary>
        public string? DeviceResourceId { get; set; }

        /// <summary>Ring ring_net_id. Null in every captured sample, so a string is a best guess.</summary>
        public string? RingNetId { get; set; }

        /// <summary>Ring active_schedule_uuid. Null in every captured sample, so a string is a best guess.</summary>
        public string? ActiveScheduleUuid { get; set; }

        /// <summary>Ring flick_elim_recommended_mode. Null in every captured sample, so a string is a best guess.</summary>
        public string? FlickElimRecommendedMode { get; set; }

        /// <summary>Ring top-level ext_power_state. Null in every captured sample, so an int is a best guess (the health object sends an int).</summary>
        public int? ExtPowerState { get; set; }

        /// <summary>Ring encryption_group.id. Null in every captured sample, so a string is a best guess.</summary>
        public string? EncryptionGroupId { get; set; }

        /// <summary>Ring encryption_group.account_group_revision_id. Null in every captured sample, so a string is a best guess.</summary>
        public string? AccountGroupRevisionId { get; set; }

        /// <summary>UTC time this row was written by a sync (not a Ring value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
