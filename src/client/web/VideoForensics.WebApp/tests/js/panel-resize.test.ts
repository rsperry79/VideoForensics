import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { loadClassicScript, uiScriptsRoot } from "./load-script";

// Drag-to-resize for MainLayout's right panel. The width must clamp to [min, max] and the final
// width must reach .NET, or the layout preference silently stops matching what the operator set.

describe("vfPanelResize", () => {
    let handle: HTMLElement;
    let panel: HTMLElement;

    beforeAll(() => {
        loadClassicScript(`${uiScriptsRoot}/panel-resize.js`);
    });

    beforeEach(() => {
        document.body.innerHTML = `<div id="handle"></div><div id="panel"></div>`;
        handle = document.getElementById("handle")!;
        panel = document.getElementById("panel")!;
        // jsdom has no layout, so the starting width is supplied directly.
        panel.getBoundingClientRect = () => ({ width: 200 }) as DOMRect;
    });

    afterEach(() => {
        window.vfPanelResize.detach("handle");
        vi.restoreAllMocks();
    });

    function drag(startX: number, endX: number) {
        handle.dispatchEvent(new MouseEvent("mousedown", { clientX: startX, bubbles: true }));
        document.dispatchEvent(new MouseEvent("mousemove", { clientX: endX }));
        document.dispatchEvent(new MouseEvent("mouseup", { clientX: endX }));
    }

    it("grows the panel when the handle is dragged left, and reports the final width", () => {
        const invokeMethodAsync = vi.fn(async () => undefined);
        window.vfPanelResize.attach("handle", "panel", null, { invokeMethodAsync } as never);

        drag(300, 250);

        expect(panel.style.width).toBe("250px");
        expect(invokeMethodAsync).toHaveBeenCalledWith("OnRightPanelResized", 250);
    });

    it("clamps to the default maximum of 500px", () => {
        window.vfPanelResize.attach("handle", "panel", null, null);

        drag(300, 0);

        expect(panel.style.width).toBe("500px");
    });

    it("clamps to the configured minimum", () => {
        window.vfPanelResize.attach("handle", "panel", { min: 180, max: 400 }, null);

        drag(300, 600);

        expect(panel.style.width).toBe("180px");
    });

    it("sets the body cursor while dragging and restores it on release", () => {
        window.vfPanelResize.attach("handle", "panel", null, null);

        handle.dispatchEvent(new MouseEvent("mousedown", { clientX: 300 }));
        expect(document.body.style.cursor).toBe("col-resize");

        document.dispatchEvent(new MouseEvent("mouseup", { clientX: 300 }));
        expect(document.body.style.cursor).toBe("");
    });

    it("stops responding to drags after detach", () => {
        window.vfPanelResize.attach("handle", "panel", null, null);
        window.vfPanelResize.detach("handle");

        drag(300, 100);

        expect(panel.style.width).toBe("");
    });
});
