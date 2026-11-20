"use strict";
// Viewport width detection and live change notification for DefaultViewportService's
// mobile/desktop form-factor detection.
// Keeps the review address in step with the page state. replaceState changes the address bar without a navigation,
// so no Blazor navigation (and no hidden JS call) runs for each state change.
window.vfHistory = {
    replaceUrl: function (url) {
        history.replaceState(history.state, '', url);
    }
};

window.vfViewport = {
    getIsMobile: function () {
        try {
            return window.matchMedia && window.matchMedia('(max-width: 599px)').matches;
        }
        catch {
            return false;
        }
    },
    getSize: function () {
        return { width: window.innerWidth || 0, height: window.innerHeight || 0 };
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

            // Window size: one notification per animation frame, so a drag-resize does not flood the circuit.
            let frame = 0;
            window.addEventListener('resize', function () {
                if (frame) {
                    return;
                }
                frame = requestAnimationFrame(function () {
                    frame = 0;
                    dotNetRef.invokeMethodAsync('OnViewportSizeChanged', window.innerWidth, window.innerHeight).catch(function () { });
                });
            });
        }
        catch {
            // Best-effort - viewport won't live-update on hosts where matchMedia isn't available;
            // the initial getIsMobile() snapshot still applies.
        }
    }
};
