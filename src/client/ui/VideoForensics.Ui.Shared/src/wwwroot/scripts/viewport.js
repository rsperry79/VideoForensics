"use strict";
// Viewport width detection and live change notification for DefaultViewportService's
// mobile/desktop form-factor detection.
window.vfViewport = {
    getIsMobile: function () {
        try {
            return window.matchMedia && window.matchMedia('(max-width: 599px)').matches;
        }
        catch (e) {
            return false;
        }
    },
    watchViewport: function (dotNetRef) {
        try {
            const mql = window.matchMedia('(max-width: 599px)');
            const listener = function (e) {
                // The circuit can be gone when the media query fires; nothing is left to notify.
                dotNetRef.invokeMethodAsync('OnViewportChanged', e.matches).catch(function () { });
            };
            if (mql.addEventListener) {
                mql.addEventListener('change', listener);
            }
            else if (mql.addListener) {
                // Older WebView2/Safari fallback.
                mql.addListener(listener);
            }
        }
        catch (e) {
            // Best-effort - viewport won't live-update on hosts where matchMedia isn't available;
            // the initial getIsMobile() snapshot still applies.
        }
    }
};
