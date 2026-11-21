// Device-local (not per-operator) persistence for the docked layout's left-nav/right-panel
// collapse state - see MainLayout's Services/LayoutPreferencesState.cs.
window.vfLayoutPrefs = {
    get: function (key: string): string | null {
        try {
            return window.localStorage.getItem(key);
        } catch {
            return null;
        }
    },
    set: function (key: string, value: string): void {
        try {
            window.localStorage.setItem(key, value);
        } catch {
            // Best-effort - private browsing / storage disabled just means the default is used.
        }
    },
    // Hides or shows the left-nav pane (the first splitter pane) and the separator after it. The pane keeps its width, so
    // showing it again brings back the saved width. Collapsing the splitter pane instead resets that width.
    setLeftNavHidden: function (hidden: boolean): boolean {
        const pane = document.querySelector<HTMLElement>('.splitter-left-nav');
        if (!pane) {
            return false;
        }

        // A class, not an inline style: Syncfusion rewrites the pane's style attribute on its own renders.
        pane.classList.toggle('vf-left-nav-hidden', hidden);
        const separator = pane.nextElementSibling;
        if (separator && separator.classList.contains('e-splitter-separator')) {
            separator.classList.toggle('vf-left-nav-hidden', hidden);
        }

        return true;
    }
};
