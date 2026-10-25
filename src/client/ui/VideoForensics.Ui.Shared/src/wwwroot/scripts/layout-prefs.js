"use strict";
// Device-local (not per-operator) persistence for the docked layout's left-nav/right-panel
// collapse state - see MainLayout's Services/LayoutPreferencesState.cs.
window.vfLayoutPrefs = {
    get: function (key) {
        try {
            return window.localStorage.getItem(key);
        }
        catch {
            return null;
        }
    },
    set: function (key, value) {
        try {
            window.localStorage.setItem(key, value);
        }
        catch {
            // Best-effort - private browsing / storage disabled just means the default is used.
        }
    }
};
