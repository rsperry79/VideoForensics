// Seeks a review video to an offset. Waits for metadata first, since setting currentTime before it loads is ignored.
window.vfSetVideoTime = function (element: HTMLVideoElement | null, seconds: number): void {
    if (!element) {
        return;
    }

    const apply = function (): void {
        element.currentTime = seconds;
    };

    if (element.readyState >= 1) {
        apply();
    } else {
        element.addEventListener('loadedmetadata', apply, { once: true });
    }
};

// Reads the current playback offset in seconds. Used by the camera grid to detect drift while tiles play.
window.vfGetVideoTime = function (element: HTMLVideoElement | null): number {
    return element ? element.currentTime : 0;
};
