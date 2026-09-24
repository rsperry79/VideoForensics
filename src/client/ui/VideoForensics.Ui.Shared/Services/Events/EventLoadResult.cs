using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Services.Events;

/// <summary>
/// Result of loading events for a forensic scope, including rows and metadata.
/// </summary>
public class EventLoadResult
{
    public required IReadOnlyList<EventRow> Rows { get; init; }
    public required IReadOnlyDictionary<Guid, LegalHold> ActiveHoldsByMediaItemId { get; init; }
    public required IReadOnlyList<IntegrityRecord> IntegrityRecords { get; init; }
    public required IReadOnlyDictionary<Guid, string> PerDeviceErrors { get; init; }
}
