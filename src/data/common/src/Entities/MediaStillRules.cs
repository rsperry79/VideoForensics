namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Classifies stored media by file name. A still (JPEG or PNG snapshot) is judged on its own terms, so the
    /// bit-rate measurement that suits video is never applied to it.
    /// </summary>
    public static class MediaStillRules
    {
        /// <summary>Returns true when the file name ends in a still-image extension (.jpg, .jpeg or .png), in any letter case.</summary>
        /// <param name="fileName">The stored media file name. Null or empty is not a still.</param>
        /// <returns>True for a still image; false for video or when there is no name.</returns>
        public static bool IsStill(string? fileName)
        {
            return !string.IsNullOrEmpty(fileName) && (fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        }
    }
}
