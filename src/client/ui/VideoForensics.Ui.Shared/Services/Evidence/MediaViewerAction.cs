namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// Actions that can be triggered by keyboard input in the media viewer.
/// </summary>
public enum MediaViewerAction
{
    /// <summary>No action.</summary>
    None,

    /// <summary>Navigate to previous item.</summary>
    Previous,

    /// <summary>Navigate to next item.</summary>
    Next,

    /// <summary>Close the viewer.</summary>
    Close,

    /// <summary>Zoom in (images only).</summary>
    ZoomIn,

    /// <summary>Zoom out (images only).</summary>
    ZoomOut,

    /// <summary>Toggle video playback.</summary>
    TogglePlay,

    /// <summary>Step video back one frame.</summary>
    FrameBack,

    /// <summary>Step video forward one frame.</summary>
    FrameForward
}
