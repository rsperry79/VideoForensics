import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { loadClassicScript, uiScriptsRoot } from "./load-script";
import { downloadText, scrollToBottom } from "../../src/client/ui/VideoForensics.Ui.Shared/src/wwwroot/scripts/server-logs.js";
import { focusElement, getCurrentTimeMs, setPlaybackRate, stepFrame, togglePlay } from "../../src/client/ui/VideoForensics.Ui.Shared/src/wwwroot/scripts/media-viewer.js";

// Small interop helpers. Each one guards a specific behavior the Blazor pages rely on, so the
// tests pin those guards rather than just the happy path.

// A stand-in for HTMLVideoElement: the helpers only read tagName and the playback fields.
function fakeVideo(overrides: Partial<Record<string, unknown>> = {}) {
    return {
        tagName: "VIDEO",
        currentTime: 0,
        duration: 10,
        paused: true,
        playbackRate: 1,
        pause: vi.fn(),
        play: vi.fn(() => Promise.resolve()),
        ...overrides,
    } as unknown as HTMLVideoElement;
}

describe("media-viewer", () => {
    it("setPlaybackRate sets the rate on a video and ignores anything else", () => {
        const video = fakeVideo();
        setPlaybackRate(video, 1.5);
        expect(video.playbackRate).toBe(1.5);

        const notVideo = { tagName: "DIV", playbackRate: 1 } as unknown as HTMLVideoElement;
        setPlaybackRate(notVideo, 2);
        expect(notVideo.playbackRate).toBe(1);
    });

    it("stepFrame pauses and clamps the seek to the start of the video", () => {
        const video = fakeVideo({ currentTime: 1 });
        stepFrame(video, -5);
        expect(video.pause).toHaveBeenCalledOnce();
        expect(video.currentTime).toBe(0);
    });

    it("stepFrame clamps the seek to the duration", () => {
        const video = fakeVideo({ currentTime: 9 });
        stepFrame(video, 5);
        expect(video.currentTime).toBe(10);
    });

    it("togglePlay plays when paused and pauses when playing", () => {
        const paused = fakeVideo({ paused: true });
        togglePlay(paused);
        expect(paused.play).toHaveBeenCalledOnce();

        const playing = fakeVideo({ paused: false });
        togglePlay(playing);
        expect(playing.pause).toHaveBeenCalledOnce();
    });

    it("togglePlay swallows a rejected play() such as an autoplay block", async () => {
        const video = fakeVideo({ paused: true, play: vi.fn(() => Promise.reject(new Error("blocked"))) });
        expect(() => togglePlay(video)).not.toThrow();
        await Promise.resolve();
    });

    it("getCurrentTimeMs converts seconds to whole milliseconds and returns 0 for a missing element", () => {
        expect(getCurrentTimeMs(fakeVideo({ currentTime: 1.2 }))).toBe(1200);
        expect(getCurrentTimeMs(null)).toBe(0);
    });

    it("focusElement focuses the element it is given", () => {
        const button = document.createElement("button");
        const focus = vi.spyOn(button, "focus");
        focusElement(button);
        expect(focus).toHaveBeenCalledOnce();
    });
});

describe("review-player (vfSetVideoTime)", () => {
    beforeAll(() => {
        loadClassicScript(`${uiScriptsRoot}/review-player.js`);
    });

    it("sets currentTime immediately when metadata is already loaded", () => {
        const video = fakeVideo({ readyState: 1 });
        window.vfSetVideoTime(video, 42);
        expect(video.currentTime).toBe(42);
    });

    it("waits for loadedmetadata when the video has not loaded yet", () => {
        let onLoaded: (() => void) | undefined;
        const video = {
            readyState: 0,
            currentTime: 0,
            addEventListener: vi.fn((_: string, handler: () => void) => { onLoaded = handler; }),
        } as unknown as HTMLVideoElement;

        window.vfSetVideoTime(video, 7);
        expect(video.currentTime).toBe(0);

        onLoaded!();
        expect(video.currentTime).toBe(7);
    });

    it("vfGetVideoTime reads the current offset and returns 0 for a missing element", () => {
        expect(window.vfGetVideoTime(fakeVideo({ currentTime: 3.5 }))).toBe(3.5);
        expect(window.vfGetVideoTime(null)).toBe(0);
    });
});

describe("layout-prefs", () => {
    beforeAll(() => {
        loadClassicScript(`${uiScriptsRoot}/layout-prefs.js`);
    });

    afterEach(() => {
        vi.restoreAllMocks();
        localStorage.clear();
    });

    it("round-trips a value through localStorage", () => {
        window.vfLayoutPrefs.set("nav.collapsed", "true");
        expect(window.vfLayoutPrefs.get("nav.collapsed")).toBe("true");
    });

    it("returns null instead of throwing when storage is unavailable", () => {
        vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new Error("denied"); });
        expect(window.vfLayoutPrefs.get("anything")).toBeNull();
    });

    it("does not throw when storage refuses a write", () => {
        vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new Error("quota"); });
        expect(() => window.vfLayoutPrefs.set("k", "v")).not.toThrow();
    });
});

describe("device-label", () => {
    beforeAll(() => {
        loadClassicScript(`${uiScriptsRoot}/device-label.js`);
    });

    const setUserAgent = (ua: string) =>
        Object.defineProperty(navigator, "userAgent", { value: ua, configurable: true });

    it("names Chrome on Windows", () => {
        setUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        expect(window.vfDeviceLabel.get()).toBe("Chrome on Windows");
    });

    it("prefers Edge over the Chrome token Edge also carries", () => {
        setUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36 Edg/120.0");
        expect(window.vfDeviceLabel.get()).toBe("Edge on Windows");
    });

    it("reports iOS rather than macOS for an iPhone Safari user agent", () => {
        setUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");
        expect(window.vfDeviceLabel.get()).toBe("Safari on iOS");
    });
});

describe("server-logs", () => {
    beforeEach(() => {
        vi.stubGlobal("URL", Object.assign(URL, {
            createObjectURL: vi.fn(() => "blob:fake"),
            revokeObjectURL: vi.fn(),
        }));
    });

    afterEach(() => {
        vi.unstubAllGlobals();
        vi.restoreAllMocks();
    });

    it("scrollToBottom moves the scroll position to the end of the element", () => {
        const el = document.createElement("div");
        Object.defineProperty(el, "scrollHeight", { value: 500, configurable: true });
        scrollToBottom(el);
        expect(el.scrollTop).toBe(500);
    });

    it("downloadText clicks a hidden anchor with the file name, then revokes the object URL", () => {
        const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);

        downloadText("server.log", "line one");

        // mock.contexts records the element each click() was called on, without aliasing `this`.
        const clicked = click.mock.contexts[0] as HTMLAnchorElement;
        expect(clicked.download).toBe("server.log");
        expect(clicked.href).toBe("blob:fake");
        expect(clicked.style.display).toBe("none");
        expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:fake");
    });
});
