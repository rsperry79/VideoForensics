namespace VideoForensics.Client.Ui.Shared.Components.Review;

/// <summary>What the media region shows for a flag's clip: the clip itself, or the reason there is none.</summary>
public enum MediaFitState
{
    /// <summary>The clip is available and plays in the media player.</summary>
    Playable,

    /// <summary>The clip is a still image; the region shows it, not a player.</summary>
    Still,

    /// <summary>The clip was deleted by the retention policy.</summary>
    Purged,

    /// <summary>The clip is recorded but its media file is not in storage.</summary>
    FileMissing,

    /// <summary>The camera that recorded the clip has been deleted.</summary>
    DeviceDeleted,

    /// <summary>The flag the review was opened for no longer exists.</summary>
    FlagNotFound,

    /// <summary>No clip was recorded in the requested window.</summary>
    NoClipInHour,
}
