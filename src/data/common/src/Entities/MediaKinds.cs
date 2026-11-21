namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// What a media row holds. Part of the unique key with the device and the recorded instant, so a snapshot, a clip
    /// and a time-lapse loop can share a start time while two rows of one kind cannot.
    /// </summary>
    public static class MediaKinds
    {
        /// <summary>A video clip pulled from the provider.</summary>
        public const string Clip = "Clip";

        /// <summary>A still captured by the snapshot service or downloaded as an event image.</summary>
        public const string Snapshot = "Snapshot";

        /// <summary>A time-lapse loop built from snapshots.</summary>
        public const string Timelapse = "Timelapse";

        /// <summary>A still captured by the user from a live view.</summary>
        public const string Still = "Still";
    }
}
