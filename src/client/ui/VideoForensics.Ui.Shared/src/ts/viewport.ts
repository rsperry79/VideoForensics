// Viewport width detection and live change notification for DefaultViewportService's
// mobile/desktop form-factor detection.
window.vfViewport = {
    getIsMobile: function (): boolean {
        try {
            return window.matchMedia && window.matchMedia('(max-width: 599px)').matches;
        } catch {
            return false;
        }
    },
    getSize: function (): { width: number; height: number } {
        return { width: window.innerWidth || 0, height: window.innerHeight || 0 };
    },
    watchViewport: function (dotNetRef: DotNetObjectReference): void {
        try {
            const mql = window.matchMedia('(max-width: 599px)');
            const listener = function (e: MediaQueryListEvent): void {
                // The circuit can be gone when the media query fires; nothing is left to notify.
                dotNetRef.invokeMethodAsync('OnViewportChanged', e.matches).catch(function () { });
            };
            if (mql.addEventListener) {
                mql.addEventListener('change', listener);
            } else if (mql.addListener) {
                // Older WebView2/Safari fallback.
                mql.addListener(listener);
            }

            // Window size: one notification per animation frame, so a drag-resize does not flood the circuit.
            let frame = 0;
            window.addEventListener('resize', function (): void {
                if (frame) {
                    return;
                }
                frame = requestAnimationFrame(function (): void {
                    frame = 0;
                    dotNetRef.invokeMethodAsync('OnViewportSizeChanged', window.innerWidth, window.innerHeight).catch(function () { });
                });
            });
        } catch {
            // Best-effort - viewport won't live-update on hosts where matchMedia isn't available;
            // the initial getIsMobile() snapshot still applies.
        }
    }
};

// Keeps the review address in step with the page state. replaceState changes the address bar without a navigation.
window.vfHistory = {
    replaceUrl: function (url: string): void {
        history.replaceState(history.state, '', url);
    }
};
