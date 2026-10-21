namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire payload for the "EvidenceChanged" push: new or updated media or events were written for this device.
    /// Open Evidence views reload on it instead of waiting for an operator to press Refresh.
    /// </summary>
    public sealed record EvidenceChangedDto(Guid DeviceId);
}
