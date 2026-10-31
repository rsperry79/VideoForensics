using VideoForensics.Client.Common.Viewer;

namespace VideoForensics.Client.Ui.Shared.Components.Viewer
{
    /// <summary>One camera in a multi-camera grid: its label, its clips on the shared timeline, and how to get each clip's video URL.</summary>
    public sealed record CameraTile(string Label, IReadOnlyList<ClipWindow> Clips, Func<string, string?> ContentUrlFor, Func<string, string?>? PreviewUrlFor = null);
}
