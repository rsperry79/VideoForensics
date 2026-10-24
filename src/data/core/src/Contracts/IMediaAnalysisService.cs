namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Measures a downloaded clip, judges it against its device's own history, and stores the red flags.</summary>
    public interface IMediaAnalysisService
    {
        /// <summary>Analyses one media item and saves the result. A missing item is ignored.</summary>
        Task AnalyzeAsync(Guid mediaItemId, CancellationToken ct);
    }
}
