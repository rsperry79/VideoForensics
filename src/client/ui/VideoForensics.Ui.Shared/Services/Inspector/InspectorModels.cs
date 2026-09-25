namespace VideoForensics.Ui.Shared.Services.Inspector
{
    /// <summary>
    /// Represents a hyperlink in the Inspector's Related tab.
    /// </summary>
    public sealed record InspectorLink(string Text, string Href);

    /// <summary>
    /// Data displayed in the Inspector panel for a selected item.
    /// </summary>
    public sealed record InspectorModel(
        string Title,
        object? Fields = null,
        string? RawJson = null,
        IReadOnlyList<InspectorLink>? Related = null,
        IReadOnlyList<KeyValuePair<string, string>>? Provenance = null);

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
