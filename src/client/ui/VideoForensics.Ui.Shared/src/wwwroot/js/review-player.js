// Seeks a review video to an offset. Waits for metadata first, since setting currentTime before it loads is ignored.
window.vfSetVideoTime = function (element, seconds) {
    if (!element) {
        return;
    }

    const apply = function () {
        element.currentTime = seconds;
    };

    if (element.readyState >= 1) {
        apply();
    } else {
        element.addEventListener('loadedmetadata', apply, { once: true });
    }
};

// Reads the current playback offset in seconds. Used by the camera grid to detect drift while tiles play.
window.vfGetVideoTime = function (element) {
    return element ? element.currentTime : 0;
};
