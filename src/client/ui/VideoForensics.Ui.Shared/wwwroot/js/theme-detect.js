// OS/browser dark-mode detection + live change notification for ThemePreferenceService's
// "System" theme mode.
window.vfTheme = {
    getPrefersDark: function () {
        try {
            return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        } catch (e) {
            return false;
        }
    },
    watchPrefersDark: function (dotNetRef) {
        try {
            var mql = window.matchMedia('(prefers-color-scheme: dark)');
            var listener = function (e) {
                dotNetRef.invokeMethodAsync('OnSystemThemeChanged', e.matches);
            };
            if (mql.addEventListener) {
                mql.addEventListener('change', listener);
            } else if (mql.addListener) {
                // Older WebView2/Safari fallback.
                mql.addListener(listener);
            }
        } catch (e) {
            // Best-effort - System mode simply won't live-update on hosts where matchMedia isn't
            // available; the initial getPrefersDark() snapshot still applies.
        }
    },
    setStylesheet: function (themeBaseName) {
        try {
            var href = '_content/Radzen.Blazor/css/' + themeBaseName + '-base.css';
            var link = document.getElementById('radzen-theme-link') || document.getElementById('vf-radzen-theme-link');
            if (!link) {
                link = document.createElement('link');
                link.id = 'vf-radzen-theme-link';
                link.rel = 'stylesheet';
                document.head.appendChild(link);
            }
            if (!link.href.endsWith(href)) {
                link.href = href;
            }
        } catch (e) {
            // Best-effort - page keeps whatever theme was last successfully applied.
        }
    }
};
