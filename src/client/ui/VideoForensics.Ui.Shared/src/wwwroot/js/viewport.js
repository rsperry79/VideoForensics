// Viewport width detection and live change notification for DefaultViewportService's
// mobile/desktop form-factor detection.
window.vfViewport = {
    getIsMobile: function () {
        try {
            return window.matchMedia && window.matchMedia('(max-width: 599px)').matches;
        } catch (e) {
            return false;
        }
    },
    watchViewport: function (dotNetRef) {
        try {
            var mql = window.matchMedia('(max-width: 599px)');
            var listener = function (e) {
                dotNetRef.invokeMethodAsync('OnViewportChanged', e.matches);
            };
            if (mql.addEventListener) {
                mql.addEventListener('change', listener);
            } else if (mql.addListener) {
                // Older WebView2/Safari fallback.
                mql.addListener(listener);
            }
        } catch (e) {
            // Best-effort - viewport won't live-update on hosts where matchMedia isn't available;
            // the initial getIsMobile() snapshot still applies.
        }
    }
};
