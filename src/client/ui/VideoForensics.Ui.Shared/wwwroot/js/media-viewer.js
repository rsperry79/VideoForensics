/**
 * Media viewer JavaScript interop module.
 * Exports functions for video playback control.
 */

/**
 * Set the playback rate of a video element.
 * @param {HTMLVideoElement} element - The video element
 * @param {number} rate - The playback rate (0.25, 0.5, 1, 1.5, 2, etc.)
 */
export function setPlaybackRate(element, rate) {
    if (element && element.tagName === 'VIDEO') {
        element.playbackRate = rate;
    }
}

/**
 * Step a video forward or backward by a specific number of seconds.
 * Pauses the video before seeking.
 * @param {HTMLVideoElement} element - The video element
 * @param {number} seconds - The number of seconds to step (positive forward, negative backward)
 */
export function stepFrame(element, seconds) {
    if (element && element.tagName === 'VIDEO') {
        element.pause();
        const newTime = Math.max(0, Math.min(element.currentTime + seconds, element.duration || Infinity));
        element.currentTime = newTime;
    }
}

/**
 * Toggle play/pause on a video element.
 * @param {HTMLVideoElement} element - The video element
 */
export function togglePlay(element) {
    if (element && element.tagName === 'VIDEO') {
        if (element.paused) {
            element.play().catch(() => {
                // Ignore play errors (e.g., if autoplay is blocked)
            });
        } else {
            element.pause();
        }
    }
}

/**
 * Focus an element to ensure keyboard events are captured.
 * @param {HTMLElement} element - The element to focus
 */
export function focusElement(element) {
    if (element) {
        element.focus();
    }
}
