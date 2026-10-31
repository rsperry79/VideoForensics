"use strict";
// OS/browser dark-mode detection + live change notification for ThemePreferenceService's
// "System" theme mode, plus the device-local copy of the chosen mode that lets the first paint
// match the last session before Blazor has loaded the operator's stored preference.
window.vfTheme = {
    getPrefersDark: function () {
        try {
            return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        }
        catch {
            return false;
        }
    },
    getStoredMode: function () {
        try {
            return window.localStorage.getItem('vf-theme-mode');
        }
        catch {
            return null;
        }
    },
    setStoredMode: function (mode) {
        try {
            window.localStorage.setItem('vf-theme-mode', mode);
        }
        catch {
            // Best-effort - private browsing / storage disabled just means the next load starts from System.
        }
    },
    getStoredTimeFormat: function () {
        try {
            return window.localStorage.getItem('vf-time-format');
        }
        catch {
            return null;
        }
    },
    setStoredTimeFormat: function (clock) {
        try {
            window.localStorage.setItem('vf-time-format', clock);
        }
        catch {
            // Best-effort - the operator's stored choice is applied once the page loads.
        }
    },
    watchPrefersDark: function (dotNetRef) {
        try {
            const mql = window.matchMedia('(prefers-color-scheme: dark)');
            const listener = function (e) {
                // The circuit can be gone when the media query fires; nothing is left to notify.
                dotNetRef.invokeMethodAsync('OnSystemThemeChanged', e.matches).catch(function () { });
            };
            if (mql.addEventListener) {
                mql.addEventListener('change', listener);
            }
            else if (mql.addListener) {
                // Older WebView2/Safari fallback.
                mql.addListener(listener);
            }
        }
        catch {
            // Best-effort - System mode simply won't live-update on hosts where matchMedia isn't
            // available; the initial getPrefersDark() snapshot still applies.
        }
    },
    setStylesheet: function (isDark) {
        try {
            const href = isDark ? '_content/Syncfusion.Blazor.Themes/bootstrap5-dark.css' : '_content/Syncfusion.Blazor.Themes/bootstrap5.css';
            let link = document.getElementById('vf-syncfusion-theme-link');
            if (!link) {
                link = document.createElement('link');
                link.id = 'vf-syncfusion-theme-link';
                link.rel = 'stylesheet';
                document.head.appendChild(link);
            }
            if (!link.href.endsWith(href)) {
                link.href = href;
            }
            document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
        }
        catch {
            // Best-effort - page keeps whatever theme was last successfully applied.
        }
    }
};
// Runs before Blazor starts: a remembered Light/Dark wins, otherwise the OS preference applies.
// This is what keeps the first paint in step with the last session.
(function applyStoredTheme() {
    const stored = window.vfTheme.getStoredMode();
    const isDark = stored === 'Dark' ? true : stored === 'Light' ? false : window.vfTheme.getPrefersDark();
    window.vfTheme.setStylesheet(isDark);
})();
