"use strict";
// Publishes the window size as CSS custom properties (--vf-vw, --vf-vh) on the root element, so layout can
// fill exactly the visible window and never overflow it. Uses the browser's ResizeObserver: no package needed.
window.vfFit = {
    start: function () {
        const root = document.documentElement;
        const publish = function () {
            root.style.setProperty('--vf-vw', (window.innerWidth || 0) + 'px');
            root.style.setProperty('--vf-vh', (window.innerHeight || 0) + 'px');
        };
        publish();
        if (typeof ResizeObserver !== 'undefined') {
            // Observing the root reports every size change of the window, including a drag-resize.
            new ResizeObserver(publish).observe(root);
        }
        else {
            window.addEventListener('resize', publish);
        }
    }
};
