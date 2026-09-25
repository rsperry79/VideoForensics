namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Maps keyboard inputs to media viewer actions.
/// </summary>
public static class MediaViewerKeys
{
    /// <summary>
    /// Map a keyboard key to a media viewer action, based on whether viewing an image or video.
    /// </summary>
    /// <param name="key">The key code or string from KeyboardEventArgs.Key.</param>
    /// <param name="isVideo">True if viewing video; false if viewing image.</param>
    /// <returns>The corresponding action, or None if the key is not mapped.</returns>
    public static MediaViewerAction Map(string? key, bool isVideo)
    {
        if (string.IsNullOrEmpty(key))
        {
            return MediaViewerAction.None;
        }

        // Navigation keys (work for both image and video)
        if (key == "ArrowLeft")
            return MediaViewerAction.Previous;
        if (key == "ArrowRight")
            return MediaViewerAction.Next;
        if (key == "Escape")
            return MediaViewerAction.Close;

        // Image-only keys (zoom)
        if (!isVideo)
        {
            if (key == "+" || key == "=")
                return MediaViewerAction.ZoomIn;
            if (key == "-")
                return MediaViewerAction.ZoomOut;
        }

        // Video-only keys
        if (isVideo)
        {
            if (key == " ")
                return MediaViewerAction.TogglePlay;
            if (key == ",")
                return MediaViewerAction.FrameBack;
            if (key == ".")
                return MediaViewerAction.FrameForward;
        }

        return MediaViewerAction.None;
    }
}
