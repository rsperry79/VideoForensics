namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// One stored snapshot attempt, reduced to what still mapping needs. The Core providers do not reference the data
    /// layer, so the caller converts its stored capture rows into this shape.
    /// </summary>
    /// <param name="DeviceId">Internal device GUID the attempt belongs to.</param>
    /// <param name="CapturedAt">When the attempt was made.</param>
    /// <param name="HttpStatus">HTTP status of the attempt. Only 200 can yield a still; any other status is a failed attempt that ends a hold.</param>
    /// <param name="MediaFilePath">Media path relative to the snapshot root. Null or blank yields no still.</param>
    public sealed record SnapshotStillRecord(Guid DeviceId, DateTimeOffset CapturedAt, int HttpStatus, string? MediaFilePath);

    /// <summary>Input to <see cref="ISnapshotStillMapper.MapCurrentStills"/>.</summary>
    /// <param name="Records">
    /// Every attempt, 200 and non-200, for any devices, in any order. Only this device's attempts are used: a 200 with an
    /// existing media file becomes a still, and a non-200 ends the hold of the still before it.
    /// </param>
    /// <param name="DeviceId">The device whose stills to map.</param>
    /// <param name="SnapshotRoot">Directory that <see cref="SnapshotStillRecord.MediaFilePath"/> is relative to.</param>
    /// <param name="LastStillHold">
    /// How long the newest still is held. It has no later capture to end it, so the caller supplies the hold. Must be positive.
    /// </param>
    public sealed record SnapshotStillMapRequest(IReadOnlyList<SnapshotStillRecord> Records, Guid DeviceId, string SnapshotRoot, TimeSpan LastStillHold);

    /// <summary>
    /// Turns stored snapshot attempts into timeline stills. An attempt is a current still only when it returned 200 and its
    /// media file exists. A failed (non-200) attempt yields no still and ends the hold of the still before it, so outages
    /// show as gaps rather than a held frame. Each still is held until the earlier of the next current still or the next
    /// failed attempt; the newest still with no failure after it uses the request's last-still hold.
    /// </summary>
    public interface ISnapshotStillMapper
    {
        /// <summary>Maps one device's snapshot attempts to timeline stills, oldest first.</summary>
        /// <param name="request">The records, device, snapshot root and last-still hold.</param>
        /// <returns>Stills ready for <see cref="ITimelineVideoBuilder.BuildAsync"/>. Empty when no attempt is current.</returns>
        /// <exception cref="ArgumentNullException">The request or its record list is null.</exception>
        /// <exception cref="ArgumentException">The snapshot root is blank or the last-still hold is not positive.</exception>
        IReadOnlyList<TimelineStill> MapCurrentStills(SnapshotStillMapRequest request);
    }
}
