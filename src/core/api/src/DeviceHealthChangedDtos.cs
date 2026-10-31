namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire payload for the "DeviceHealthChanged" push: a health reading was recorded for this device.
    /// Open device views refresh their status on it instead of waiting for a page reload.
    /// </summary>
    public sealed record DeviceHealthChangedDto(Guid DeviceId);
}
