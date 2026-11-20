namespace VideoForensics.Client.Ui.Shared.Services
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
        /// The measured window width in CSS pixels. Zero until the browser has reported a size.
        /// </summary>
        double WidthPx { get; }

        /// <summary>
        /// The measured window height in CSS pixels. Zero until the browser has reported a size.
        /// Layouts size media from this instead of fixed viewport-height guesses.
        /// </summary>
        double HeightPx { get; }

        /// <summary>
        /// Fired when the form factor changes between mobile and desktop, or when the window size changes.
        /// </summary>
        event Action? OnChange;

        /// <summary>
        /// Initializes the viewport service, reading the initial viewport width via JS interop
        /// and starting to watch for resize changes. This method is idempotent and safe to call multiple times.
        /// </summary>
        Task InitializeAsync();
    }
}
