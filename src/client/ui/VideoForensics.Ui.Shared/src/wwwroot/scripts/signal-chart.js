// Pointer measurement for the signal history chart. Blazor only hands C# the event's client
// coordinates, so the chart's on-screen box is read here and the offset is worked out in C#.
export function measure(elementId, clientX) {
    const element = document.getElementById(elementId);
    if (!element) {
        return { offset: 0, width: 0 };
    }

    const rect = element.getBoundingClientRect();
    return { offset: clientX - rect.left, width: rect.width };
}
