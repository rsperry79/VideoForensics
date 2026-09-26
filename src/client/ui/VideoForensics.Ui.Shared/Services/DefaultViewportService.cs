using Microsoft.JSInterop;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VideoForensics.Ui.Shared.Tests")]

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Default implementation of <see cref="IViewportService"/> using JS interop to detect
    /// viewport width changes via matchMedia queries.
    /// </summary>
    public class DefaultViewportService : IViewportService, IDisposable
    {
        private readonly IJSRuntime _js;
        private bool _initialized;
        private DotNetObjectReference<DefaultViewportService>? _selfRef;

        public DefaultViewportService(IJSRuntime js)
        {
            _js = js;
        }

        /// <summary>
        /// Gets whether the current viewport width is narrower than 600px (mobile).
        /// Defaults to false (desktop) until initialized.
        /// </summary>
        public bool IsMobile { get; private set; }

        /// <summary>
        /// Fired when the viewport form factor changes (IsMobile value changed).
        /// </summary>
        public event Action? OnChange;

        /// <summary>
        /// Initializes the viewport service, reading the initial viewport width via JS interop
        /// and starting to watch for resize changes. This method is idempotent.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            try
            {
                // Get initial viewport state
                IsMobile = await _js.InvokeAsync<bool>("vfViewport.getIsMobile");

                // Create a reference to this service for JS callbacks
                _selfRef = DotNetObjectReference.Create(this);

                // Start watching for viewport changes
                await _js.InvokeVoidAsync("vfViewport.watchViewport", _selfRef);
            }
            catch (JSException)
            {
                // Script not loaded on this host / pre-render pass - defaults to desktop (IsMobile = false).
            }
        }

        /// <summary>
        /// Called from JavaScript via JSInvokable when the viewport width crosses the breakpoint.
        /// Only fires the OnChange event if the value actually changed.
        /// </summary>
        [JSInvokable]
        public void OnViewportChanged(bool isMobile)
        {
            if (IsMobile != isMobile)
            {
                IsMobile = isMobile;
                OnChange?.Invoke();
            }
        }

        /// <summary>
        /// Pure helper function to determine if a given width (in pixels) is mobile.
        /// This is extracted as a static method so unit tests can directly test the breakpoint logic
        /// without involving JS interop.
        /// </summary>
        /// <param name="widthPx">Viewport width in pixels.</param>
        /// <returns>True if width is less than 600px (mobile), false otherwise.</returns>
        internal static bool DetermineIsMobile(double widthPx) => widthPx < 600;

        /// <summary>
        /// Disposes the DotNetObjectReference held for JS callbacks.
        /// </summary>
        public void Dispose()
        {
            _selfRef?.Dispose();
        }
    }
}
