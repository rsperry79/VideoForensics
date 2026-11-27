namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire form of why a snapshot attempt stored no media. Mirrors the server entity enum; the Api contracts project
    /// cannot reference the data layer, so the mapping converts between the two.
    /// </summary>
    public enum SnapshotSkipReasonDto
    {
        /// <summary>The provider reported the camera as not connected.</summary>
        Offline,

        /// <summary>The frame's hash equals the device's previous snapshot.</summary>
        IdenticalFrame,
    }
}
