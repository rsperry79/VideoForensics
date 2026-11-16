// Reads an element's position and height in CSS pixels, so a click or drag on a fill-height timeline maps to the time under the pointer.
export function measure(element) {
    if (!element) {
        return null;
    }

    const rect = element.getBoundingClientRect();
    return { top: rect.top, height: rect.height };
}
