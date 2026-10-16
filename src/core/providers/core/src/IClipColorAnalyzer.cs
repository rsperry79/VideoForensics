namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Measures a downloaded clip's colour saturation and brightness.</summary>
    public interface IClipColorAnalyzer
    {
        /// <summary>Returns the clip's stats, or null when they cannot be measured.</summary>
        Task<ClipColorStats?> AnalyzeAsync(string mediaFilePath, CancellationToken ct);
    }
}
