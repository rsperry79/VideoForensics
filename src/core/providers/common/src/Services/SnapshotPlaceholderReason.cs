namespace VideoForensics.Core.Providers.Common.Services
{
    /// <summary>Why a snapshot frame was classified as a placeholder rather than a real image.</summary>
    public enum SnapshotPlaceholderReason
    {
        /// <summary>The device reported it is not connected, so the frame cannot be live.</summary>
        Offline,

        /// <summary>The frame is byte-identical to the previous capture.</summary>
        IdenticalFrame,
    }
}
