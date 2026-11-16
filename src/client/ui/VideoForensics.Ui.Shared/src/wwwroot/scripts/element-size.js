// Reads an element's position and size in CSS pixels, so a click on a timeline maps to the time under the pointer.
export function measure(element) {
    if (!element) {
        return null;
    }

    const rect = element.getBoundingClientRect();
    return { top: rect.top, height: rect.height, left: rect.left, width: rect.width };
}
