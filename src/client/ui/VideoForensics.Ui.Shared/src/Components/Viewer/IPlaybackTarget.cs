namespace VideoForensics.Client.Ui.Shared.Components.Viewer;

/// <summary>A video the grid's transport bar can drive: play or pause it, and step it by seconds.</summary>
public interface IPlaybackTarget
{
    /// <summary>Plays when paused and pauses when playing.</summary>
    Task TogglePlayAsync();

    /// <summary>Starts playback when paused. A video already playing is left alone.</summary>
    Task PlayAsync();

    /// <summary>Steps by a number of seconds (negative steps back).</summary>
    Task StepAsync(double seconds);
}
