namespace VideoForensics.Client.Ui.Shared.Components.Viewer;

/// <summary>How the picture fills the player's box.</summary>
public enum MediaFitMode
{
    /// <summary>The whole picture is visible, with bars if the aspect differs.</summary>
    Contain,

    /// <summary>The picture fills the box and is cropped if the aspect differs.</summary>
    Cover,
}
