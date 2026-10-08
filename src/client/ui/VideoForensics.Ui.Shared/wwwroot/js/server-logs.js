// Scroll and download helpers for the SuperAdmin Server Logs page (Pages/ServerLogs.razor).

export function scrollToBottom(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

export function downloadText(fileName, text) {
    const blob = new Blob([text], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
}
