using Microsoft.AspNetCore.Components;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>
    /// Circuit-scoped slot for page-specific content rendered in the left navigation pane, below the
    /// nav items. A page sets this (typically via the <c>&lt;PageLeftPanelContent&gt;</c> wrapper
    /// component) and clears it on dispose so content doesn't leak into the next page after navigation.
    /// Content is tagged with its owner so a page can only clear content it set.
    /// </summary>
    public class LeftPanelContentService
    {
        private object? _owner;

        public RenderFragment? Content { get; private set; }

        public event Action? OnChange;

        /// <summary>Sets the panel content for <paramref name="owner"/>, replacing whatever was there.</summary>
        public void Set(RenderFragment? content, object? owner = null)
        {
            Content = content;
            _owner = owner;
            OnChange?.Invoke();
        }

        /// <summary>
        /// Clears the panel only while <paramref name="owner"/> still holds it. Blazor sets the next page's content before
        /// disposing the previous page, so an unconditional clear would wipe the next page's panel.
        /// </summary>
        public void Clear(object? owner = null)
        {
            if (Content is null || !ReferenceEquals(_owner, owner))
            {
                return;
            }

            Content = null;
            _owner = null;
            OnChange?.Invoke();
        }
    }
}
