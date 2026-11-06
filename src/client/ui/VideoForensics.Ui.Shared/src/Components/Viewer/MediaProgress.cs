namespace VideoForensics.Client.Ui.Shared.Components.Viewer;

/// <summary>A playback position report from <c>MediaPlayer.OnProgress</c>.</summary>
/// <param name="PositionSeconds">Offset into the source, in seconds.</param>
/// <param name="DurationSeconds">Source length in seconds. Null while unknown, and for a live stream.</param>
/// <param name="Utc">The absolute time at the position, or null when the player has no <c>SourceStartUtc</c>.</param>
public readonly record struct MediaProgress(double PositionSeconds, double? DurationSeconds, DateTime? Utc);
