namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire payload for the "MediaFlagChanged" push: a media flag was reviewed or re-opened. Open review pages reload the
    /// flag and its neighbours on it. <paramref name="DeviceId"/> lets a page refresh only the device it is showing.
    /// </summary>
    public sealed record MediaFlagChangedDto(Guid FlagId, Guid DeviceId);
}
