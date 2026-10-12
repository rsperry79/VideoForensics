namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>
/// UI-facing status of an in-flight or completed "grab still with hash" capture, shown by
/// <see cref="VideoForensics.Ui.Shared.Components.Evidence.MediaViewer"/>. Owned by the page
/// hosting the viewer (e.g. Evidence.razor), which calls the capture service and updates this
/// after each attempt.
/// </summary>
public class MediaStillCaptureStatus
{
    /// <summary>True while the capture request is in flight.</summary>
    public bool InProgress { get; init; }

    /// <summary>The captured still's SHA-256 hash, set on success.</summary>
    public string? Sha256Hash { get; init; }

    /// <summary>A ticketed content URL to open/view the captured still, set on success.</summary>
    public string? OpenUrl { get; init; }

    /// <summary>Error message, set when the capture failed.</summary>
    public string? ErrorMessage { get; init; }
}
