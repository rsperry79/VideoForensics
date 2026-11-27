namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Why a snapshot attempt stored no media. Null on <see cref="SnapshotCapture.SkipReason"/> means a normal attempt.</summary>
    public enum SnapshotSkipReason
    {
        /// <summary>The provider reported the camera as not connected, so the frame would be a placeholder.</summary>
        Offline,

        /// <summary>The frame's hash equals the device's previous snapshot, so it adds nothing new.</summary>
        IdenticalFrame,
    }
}
