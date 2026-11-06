namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Where a light event came from.</summary>
    public enum DeviceLightEventSource
    {
        /// <summary>Seen as on in the reported device state during a sync.</summary>
        Observed = 0,

        /// <summary>Turned on by a control request from this application.</summary>
        Command = 1
    }

    /// <summary>
    /// One light-on event for a device (a floodlight or similar). Provider agnostic. Written when a sync sees the light
    /// turn on, or when the application switches it on; never on every sync while the light stays on.
    /// </summary>
    public class DeviceLightEvent
    {
        /// <summary>Gets or sets the unique identifier of this event.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the device the light belongs to.</summary>
        public Guid DeviceId { get; set; }

        /// <summary>Gets or sets the UTC time the light was seen on or commanded on.</summary>
        public DateTime OccurredAtUtc { get; set; }

        /// <summary>Gets or sets whether the event was observed during a sync or came from a control command.</summary>
        public DeviceLightEventSource Source { get; set; }

        /// <summary>Gets or sets whether this row records the light turning on (true) or an observed off after an open-ended on (false, the off marker).</summary>
        public bool IsOn { get; set; }

        /// <summary>Gets or sets the on-time reported as remaining, in seconds. Null when none was reported (a command returns none).</summary>
        public int? SecondsRemaining { get; set; }
    }
}
