namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire DTO for one snapshot capture attempt. <see cref="IsCurrent"/> is true only for a 200 attempt, and only that attempt
    /// carries a <see cref="MediaFilePath"/>. <c>Trigger</c> is "fresh" or "latest".
    /// </summary>
    public sealed record SnapshotCaptureDto(
        Guid Id,
        Guid DeviceId,
        DateTimeOffset CapturedAtUtc,
        string Trigger,
        int HttpStatus,
        bool IsCurrent,
        string? MediaFilePath,
        Guid? DeviceHealthId);
}
