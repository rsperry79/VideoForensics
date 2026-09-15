// Hand-rolled drag-to-resize for MainLayout's right panel. Not a Syncfusion SfSplitter pane -
// see the comment above the right-panel markup in MainLayout.razor for why.
window.vfPanelResize = {
    attach: function (handleId, panelId, options, dotNetRef) {
        var handle = document.getElementById(handleId);
        var panel = document.getElementById(panelId);
        if (!handle || !panel) {
            return;
        }

        var min = (options && options.min) || 150;
        var max = (options && options.max) || 500;
        var startX = 0;
        var startWidth = 0;

        function onMouseMove(e) {
            // Dragging the handle left (toward the content) grows the right-side panel.
            var delta = startX - e.clientX;
            var newWidth = Math.min(max, Math.max(min, startWidth + delta));
            panel.style.flexBasis = newWidth + 'px';
            panel.style.width = newWidth + 'px';
        }

        function onMouseUp() {
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
            document.body.style.cursor = '';
            document.body.style.userSelect = '';

            var finalWidth = parseInt(panel.style.flexBasis, 10);
            if (dotNetRef && !isNaN(finalWidth)) {
                dotNetRef.invokeMethodAsync('OnRightPanelResized', finalWidth);
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
        var handle = document.getElementById(handleId);
        if (handle && handle._vfMouseDown) {
            handle.removeEventListener('mousedown', handle._vfMouseDown);
            handle._vfMouseDown = null;
        }
    }
};
