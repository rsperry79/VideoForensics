using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Services.Inspector
{
    /// <summary>
    /// Represents a hyperlink in the Inspector's Related tab.
    /// </summary>
    public sealed record InspectorLink(string Text, string Href);

    /// <summary>
    /// Represents a pinning target (event or media item) that can be pinned to a case.
    /// </summary>
    public sealed record PinTarget(CaseItemKind Kind, Guid TargetId);

    /// <summary>
    /// Evidence-management actions available for the inspected item: legal hold placement/release,
    /// device-scoped integrity verification, and export. Populated only for items that can carry
    /// downloaded media (events and media-only evidence); other inspector consumers (e.g. Query API,
    /// Self-Test) leave this null and get no action buttons.
    /// </summary>
    /// <param name="MediaItemId">The downloaded media item this item is linked to, if any. Null means
    /// no hold/verify/export action applies (e.g. an event with no downloaded media yet).</param>
    /// <param name="DeviceId">The device this item belongs to, used to scope integrity verification.</param>
    /// <param name="ActiveHoldId">The currently-active legal hold's ID, if <see cref="MediaItemId"/>
    /// is on hold; null otherwise.</param>
    public sealed record EvidenceActionState(Guid? MediaItemId, Guid DeviceId, Guid? ActiveHoldId);

    /// <summary>
    /// Data displayed in the Inspector panel for a selected item.
    /// </summary>
    public sealed record InspectorModel(
        string Title,
        object? Fields = null,
        string? RawJson = null,
        IReadOnlyList<InspectorLink>? Related = null,
        IReadOnlyList<KeyValuePair<string, string>>? Provenance = null,
        PinTarget? Pin = null,
        EvidenceActionState? Actions = null);

    /// <summary>
    /// Circuit-scoped state for the Inspector panel. Tracks the currently-inspected item
    /// and notifies subscribers when the inspector is shown or cleared.
    /// </summary>
    public class InspectorState
    {
        /// <summary>
        /// The currently-displayed inspector model, or null if the inspector is not visible.
        /// </summary>
        public InspectorModel? Current { get; private set; }

        /// <summary>
        /// Raised when Show or Clear is called and changes the visibility or content of the inspector.
        /// </summary>
        public event Action? OnChange;

        /// <summary>
        /// Display the given model in the inspector and raise OnChange.
        /// </summary>
        public void Show(InspectorModel model)
        {
            Current = model;
            OnChange?.Invoke();
        }

        /// <summary>
        /// Hide the inspector if it is currently visible. No-op (no event) if already null.
        /// </summary>
        public void Clear()
        {
            if (Current is null)
            {
                return;
            }

            Current = null;
            OnChange?.Invoke();
        }
    }
}
