namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Media kind values as they appear on <see cref="MediaItemDto.MediaKind"/>. Clients use these instead of the server's
    /// entity constants, because client projects may not reference the data layer. A test keeps them equal to the server's values.
    /// </summary>
    public static class MediaKindNames
    {
        /// <summary>A still captured by the snapshot service.</summary>
        public const string Snapshot = "Snapshot";

        /// <summary>A time-lapse loop built from snapshots.</summary>
        public const string Timelapse = "Timelapse";
    }
}
