namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Detects viewport width and provides notifications when the viewport changes between
    /// mobile (< 600px) and desktop (>= 600px) form factors.
    /// </summary>
    public interface IViewportService
    {
        /// <summary>
        /// Gets whether the current viewport width is narrower than 600px (mobile).
        /// </summary>
        bool IsMobile { get; }

        /// <summary>
        /// Fired when the viewport form factor changes between mobile and desktop.
        /// </summary>
        event Action? OnChange;

        /// <summary>
        /// Initializes the viewport service, reading the initial viewport width via JS interop
        /// and starting to watch for resize changes. This method is idempotent and safe to call multiple times.
        /// </summary>
        Task InitializeAsync();
    }
}
