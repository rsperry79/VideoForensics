namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// The per-device choice on the schedule page. <see cref="Capture"/> keeps snapshots and health, <see cref="HealthOnly"/>
    /// keeps only health telemetry, and <see cref="Off"/> stops both for that device.
    /// </summary>
    public enum DeviceCaptureMode
    {
        /// <summary>No snapshots and no health readings for this device.</summary>
        Off,

        /// <summary>Health readings only; no snapshots.</summary>
        HealthOnly,

        /// <summary>Snapshots and health readings.</summary>
        Capture,
    }
}
