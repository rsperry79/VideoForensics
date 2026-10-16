using VideoForensics.Core.Providers.Core;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Settings for media analysis. Bound from the <c>MediaAnalysis</c> configuration section.</summary>
    public sealed class MediaAnalysisOptions
    {
        /// <summary>Hemisphere of the install, used to bucket baselines by season. Defaults to northern.</summary>
        public Hemisphere Hemisphere { get; set; } = Hemisphere.Northern;
    }
}
