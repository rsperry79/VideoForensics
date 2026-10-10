using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Inspector;

namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Decision logic for the evidence-management actions moved over from the retired Events page
/// (legal hold place/release, device integrity verification, export): what's allowed for a given
/// item, and how to build the export navigation target. Kept out of Razor so it's directly
/// testable and shared by both the Evidence grid's context menu and the Inspector panel's actions.
/// </summary>
public static class EvidenceActions
{
    /// <summary>True if a legal hold can be placed: the item has downloaded media and isn't already on hold.</summary>
    public static bool CanPlaceHold(EvidenceActionState? actions) =>
        actions?.MediaItemId is not null && actions.ActiveHoldId is null;

    /// <summary>True if a legal hold can be released: the item has downloaded media and is currently on hold.</summary>
    public static bool CanReleaseHold(EvidenceActionState? actions) =>
        actions?.MediaItemId is not null && actions.ActiveHoldId is not null;

    /// <summary>
    /// True if device-scoped integrity verification can be triggered from this item. Mirrors
    /// EventRow's VerifyIntegrityForDeviceAsync guard, which required a linked media item even
    /// though the underlying call verifies every file for the device, not just this one.
    /// </summary>
    public static bool CanVerifyIntegrity(EvidenceActionState? actions) =>
        actions?.MediaItemId is not null;

    /// <summary>True if this item's media can be sent to the export flow.</summary>
    public static bool CanExport(EvidenceActionState? actions) =>
        actions?.MediaItemId is not null;

    /// <summary>
    /// The chain-of-custody actor identity to record for a legal hold place/release action: the
    /// signed-in operator's id (<c>PairedSessionState.OperatorId</c>), stringified the same way
    /// <c>CaseState.PinAsync</c> records the pinning operator, and the same way the server's
    /// <c>EventEndpoints</c> resolves <c>createdBy</c>/<c>releasedBy</c> from the operator-id claim.
    /// Both the Grid context menu and the Inspector panel call this single helper so they can't
    /// diverge. Null means no operator is signed in - the caller must refuse the action rather
    /// than falling back to a machine-level identity like <c>Environment.UserName</c> (the old
    /// Events page's bug: that recorded the server process's OS account, not the operator, and
    /// would misattribute the action in the chain of custody).
    /// </summary>
    public static string? ResolveOperatorActor(Guid? operatorId) => operatorId?.ToString();

    /// <summary>
    /// Collects the distinct, exportable (downloaded-media) IDs from a set of evidence items.
    /// Event-only items (no linked media) are skipped, mirroring Events.razor's ExportRows.
    /// </summary>
    public static IReadOnlyList<Guid> GetExportMediaItemIds(IEnumerable<EvidenceItem> items) =>
        items
            .Where(i => i.Media is not null)
            .Select(i => i.Media!.Id)
            .Distinct()
            .ToList();

    /// <summary>Builds the "/review/export?ids=..." navigation target for the given media item IDs.</summary>
    public static string BuildExportUrl(IEnumerable<Guid> mediaItemIds)
    {
        var idsParam = string.Join(",", mediaItemIds);
        return $"/review/export?ids={Uri.EscapeDataString(idsParam)}";
    }

    /// <summary>
    /// Determines the display text/color for a media item's integrity status. Mirrors
    /// ForensicReportRenderer.DetermineMediaStatus (archive/VideoForensics/ForensicReportRenderer.cs)
    /// and the copy that used to live in Events.razor.
    /// </summary>
    public static (string Text, string Color) DetermineIntegrityStatus(
        Guid mediaItemId,
        IReadOnlyList<IntegrityRecord> integrityRecords)
    {
        var record = integrityRecords.FirstOrDefault(r => r.MediaItemId == mediaItemId);

        if (record is null)
        {
            return ("Not verified", "goldenrod");
        }

        if (!record.Passed)
        {
            return ("Integrity failed", "red");
        }

        return ("Verified", "green");
    }
}
