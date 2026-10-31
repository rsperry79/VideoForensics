namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One doorbot linked to a chime (GET chimes/{id}/linked_doorbots). Rows are replaced per chime.</summary>
    public class RingChimeLinkedDoorbot
    {
        public Guid Id { get; set; }
        public Guid ChimeDeviceId { get; set; }
        public int Position { get; set; }

        /// <summary>Ring linked_doorbots[].id.</summary>
        public long LinkedDoorbotId { get; set; }

        /// <summary>Ring linked_doorbots[].device_id (text as Ring sends it).</summary>
        public string? DeviceId { get; set; }

        public string? Kind { get; set; }
        public bool? CallAlerts { get; set; }
        public bool? MotionAlerts { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
