/**
 * MediaPlayer interop module. The only script that reads or controls a <video> for the app's media playback,
 * replacing the old review-player and media-viewer readers. Every function takes the video element so one module
 * instance serves every player on the page.
 */
/** How often playback progress is reported to .NET, at most. About 4 Hz. */
const PROGRESS_INTERVAL_MS = 250;
const players = new WeakMap();
const ERROR_CODES = { 1: "aborted", 2: "network", 3: "decode", 4: "unsupported" };
function isVideo(element) {
    return !!element && element.tagName === "VIDEO";
}
function finiteOrNull(value) {
    return Number.isFinite(value) ? value : null;
}
/**
 * Starts reporting a video's events to .NET and applies the first options.
 *
 * @param element - The video element.
 * @param host - The DotNetObjectReference of the MediaPlayerJsBridge.
 * @param options - Effective playback rate, mute state and whether to report progress.
 */
export function attach(element, host, options) {
    if (!isVideo(element)) {
        return;
    }
    detach(element);
    const state = { reportProgress: options.reportProgress, lastProgressAt: 0, listeners: [] };
    const send = (method, ...args) => {
        host.invokeMethodAsync(method, ...args).catch(() => {
            // The circuit or the component is already gone, so there is nobody left to tell.
        });
    };
    const sendProgress = (force) => {
        if (!state.reportProgress) {
            return;
        }
        const now = performance.now();
        if (!force && now - state.lastProgressAt < PROGRESS_INTERVAL_MS) {
            return;
        }
        state.lastProgressAt = now;
        send("OnProgress", element.currentTime, finiteOrNull(element.duration));
    };
    const on = (name, handler) => {
        element.addEventListener(name, handler);
        state.listeners.push([name, handler]);
    };
    // loadeddata (not loadedmetadata) so a live stream also counts as ready once its first frame arrives.
    on("loadeddata", () => send("OnReady", finiteOrNull(element.duration)));
    on("ended", () => {
        sendProgress(true);
        send("OnEnded");
    });
    on("error", () => send("OnError", ERROR_CODES[element.error?.code ?? 0] ?? "unknown"));
    on("timeupdate", () => sendProgress(false));
    // A pause or seek ends in a position the throttled timeupdate may have skipped.
    on("pause", () => sendProgress(true));
    on("seeked", () => sendProgress(true));
    players.set(element, state);
    configure(element, options);
    // The element can already have data when the module attaches, in which case loadeddata will not fire again.
    if (element.readyState >= 2) {
        send("OnReady", finiteOrNull(element.duration));
    }
}
/**
 * Applies the rate, mute state and progress reporting. The default rate is set too, since a browser resets the
 * rate to it when the source changes.
 */
export function configure(element, options) {
    if (!isVideo(element)) {
        return;
    }
    element.defaultPlaybackRate = options.rate;
    element.playbackRate = options.rate;
    // Blazor renders `muted` only as an attribute, which the browser ignores after load, so the property is set here.
    element.muted = options.muted;
    const state = players.get(element);
    if (state) {
        state.reportProgress = options.reportProgress;
    }
}
/** Stops reporting a video's events. Safe to call for an element that was never attached. */
export function detach(element) {
    if (!element) {
        return;
    }
    const state = players.get(element);
    if (!state) {
        return;
    }
    for (const [name, handler] of state.listeners) {
        element.removeEventListener(name, handler);
    }
    players.delete(element);
}
/** Starts playback. A refused play (for example autoplay blocked) is not an error for the caller. */
export function play(element) {
    if (isVideo(element)) {
        element.play().catch(() => {
            // Autoplay blocked or the source is unplayable; the error event reports the latter.
        });
    }
}
export function pause(element) {
    if (isVideo(element)) {
        element.pause();
    }
}
export function togglePlay(element) {
    if (isVideo(element)) {
        if (element.paused) {
            play(element);
        }
        else {
            element.pause();
        }
    }
}
/**
 * Seeks to an offset in seconds. Waits for metadata first, since setting currentTime before it loads is ignored.
 */
export function seek(element, seconds) {
    if (!isVideo(element)) {
        return;
    }
    const apply = () => {
        element.currentTime = seconds;
    };
    if (element.readyState >= 1) {
        apply();
    }
    else {
        element.addEventListener("loadedmetadata", apply, { once: true });
    }
}
/**
 * Steps forward or backward by a number of seconds. Pauses first and clamps to the start and end of the video.
 */
export function step(element, seconds) {
    if (isVideo(element)) {
        element.pause();
        element.currentTime = Math.max(0, Math.min(element.currentTime + seconds, element.duration || Infinity));
    }
}
/** The current playback offset in seconds, or 0 when the element is missing. */
export function getCurrentTime(element) {
    return isVideo(element) ? element.currentTime : 0;
}
/** The current playback offset in whole milliseconds, or 0 when the element is missing. */
export function getCurrentTimeMs(element) {
    return isVideo(element) ? Math.round(element.currentTime * 1000) : 0;
}
