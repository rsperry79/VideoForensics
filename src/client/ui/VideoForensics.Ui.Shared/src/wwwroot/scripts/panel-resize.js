"use strict";
// Hand-rolled drag-to-resize for MainLayout's right panel. Not a Syncfusion SfSplitter pane -
// see the comment above the right-panel markup in MainLayout.razor for why.
window.vfPanelResize = {
    attach: function (handleId, panelId, options, dotNetRef) {
        const handle = document.getElementById(handleId);
        const panel = document.getElementById(panelId);
        if (!handle || !panel) {
            return;
        }
        const min = (options && options.min) || 150;
        const max = (options && options.max) || 500;
        let startX = 0;
        let startWidth = 0;
        function onMouseMove(e) {
            // Dragging the handle left (toward the content) grows the right-side panel.
            const delta = startX - e.clientX;
            const newWidth = Math.min(max, Math.max(min, startWidth + delta));
            panel.style.flexBasis = newWidth + 'px';
            panel.style.width = newWidth + 'px';
        }
        function onMouseUp() {
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
            const finalWidth = parseInt(panel.style.flexBasis, 10);
            if (dotNetRef && !isNaN(finalWidth)) {
                // The circuit can be gone at drag end; the new width is only a notification.
                dotNetRef.invokeMethodAsync('OnRightPanelResized', finalWidth).catch(function () { });
            }
        }
        function onMouseDown(e) {
            e.preventDefault();
            startX = e.clientX;
            startWidth = panel.getBoundingClientRect().width;
            document.addEventListener('mousemove', onMouseMove);
            document.addEventListener('mouseup', onMouseUp);
            document.body.style.cursor = 'col-resize';
            document.body.style.userSelect = 'none';
        }
        handle.addEventListener('mousedown', onMouseDown);
        handle._vfMouseDown = onMouseDown;
    },
    detach: function (handleId) {
        const handle = document.getElementById(handleId);
        if (handle && handle._vfMouseDown) {
            handle.removeEventListener('mousedown', handle._vfMouseDown);
            handle._vfMouseDown = null;
        }
    }
};
