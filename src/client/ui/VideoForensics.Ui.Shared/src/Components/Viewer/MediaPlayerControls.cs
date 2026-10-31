namespace VideoForensics.Client.Ui.Shared.Components.Viewer;

/// <summary>Which transport controls <c>MediaPlayer</c> shows.</summary>
public enum MediaPlayerControls
{
    /// <summary>The browser's own controls.</summary>
    Native,

    /// <summary>No controls. The host drives playback through the component's methods.</summary>
    None,
}
