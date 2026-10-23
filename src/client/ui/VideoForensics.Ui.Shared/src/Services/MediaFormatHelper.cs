namespace VideoForensics.Ui.Shared.Services;

/// <summary>
/// Helper methods for determining media format types from MIME type strings or file extensions.
/// </summary>
public static class MediaFormatHelper
{
    /// <summary>
    /// Determines if a format string represents an image type.
    /// </summary>
    /// <param name="format">The format string (e.g., MIME type or file extension).</param>
    /// <returns>True if the format is an image type; otherwise false.</returns>
    public static bool IsImage(string? format) =>
        !string.IsNullOrEmpty(format) && (
            format.Contains("jpg", StringComparison.OrdinalIgnoreCase) ||
            format.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ||
            format.Contains("png", StringComparison.OrdinalIgnoreCase) ||
            format.Contains("image", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Determines if a format string represents a video type.
    /// </summary>
    /// <param name="format">The format string (e.g., MIME type or file extension).</param>
    /// <returns>True if the format is a video type; otherwise false.</returns>
    public static bool IsVideo(string? format) =>
        !string.IsNullOrEmpty(format) && (
            format.Contains("mp4", StringComparison.OrdinalIgnoreCase) ||
            format.Contains("video", StringComparison.OrdinalIgnoreCase));
}
