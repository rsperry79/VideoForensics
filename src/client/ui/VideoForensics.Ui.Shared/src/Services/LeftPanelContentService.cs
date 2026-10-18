using Microsoft.AspNetCore.Components;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Circuit-scoped slot for page-specific content rendered in the left navigation pane, below the
    /// nav items. A page sets this (typically via the <c>&lt;PageLeftPanelContent&gt;</c> wrapper
    /// component) and clears it on dispose so content doesn't leak into the next page after navigation.
    /// </summary>
    public class LeftPanelContentService
    {
        public RenderFragment? Content { get; private set; }

        public event Action? OnChange;

        public void Set(RenderFragment? content)
        {
            Content = content;
            OnChange?.Invoke();
        }

        public void Clear()
        {
            if (Content is null)
            {
                return;
            }

            Content = null;
            OnChange?.Invoke();
        }
    }
}
