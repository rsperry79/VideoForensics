using VideoForensics.Data.Common.Entities;

using VideoForensics.Core.Api.Contracts;
namespace VideoForensics.Client.Ui.Shared.Components.Review;

/// <summary>Tells a flagged clip that is a still image (a JPEG or PNG snapshot) from a video clip.</summary>
public static class MediaStill
{
    /// <summary>The file extensions of still images. The media player cannot play these.</summary>
    private static readonly string[] StillExtensions = [".jpg", ".jpeg", ".png"];

    /// <summary>
    /// True when the clip's file name or file path ends in a still image extension (.jpg, .jpeg or .png, any case).
    /// A still is shown as an image; the media player cannot play it, and it has no timeline to scrub.
    /// </summary>
    /// <param name="clip">The media item to test.</param>
    public static bool IsStill(MediaItemDto clip)
    {
        return HasStillExtension(clip.FileName) || HasStillExtension(clip.FilePath);
    }

    private static bool HasStillExtension(string path)
    {
        return Array.Exists(StillExtensions, ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
}
