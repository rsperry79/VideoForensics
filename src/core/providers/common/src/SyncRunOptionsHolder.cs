using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Core.Providers.Common
{
    /// <summary>Default <see cref="ISyncRunOptions"/>: holds the legacy options until the runner replaces them.</summary>
    public sealed class SyncRunOptionsHolder : ISyncRunOptions
    {
        /// <inheritdoc />
        public SyncRunOptions Current { get; set; } = new();
    }
}
