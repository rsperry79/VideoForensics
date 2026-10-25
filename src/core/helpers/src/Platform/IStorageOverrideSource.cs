namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// Supplies an install-time path override for a storage category. Windows reads these from the registry
    /// (<c>Platform.Windows</c>); other platforms use the environment variables that <see cref="StorageLocationProvider"/> reads itself.
    /// </summary>
    public interface IStorageOverrideSource
    {
        /// <summary>Returns the configured root for the category, or null when none is set. Never throws.</summary>
        string? GetOverride(StorageCategory category);
    }

    /// <summary>Source that never overrides anything. The default when a host does not register one.</summary>
    public sealed class NullStorageOverrideSource : IStorageOverrideSource
    {
        /// <inheritdoc />
        public string? GetOverride(StorageCategory category) => null;
    }

    /// <summary>
    /// Process-wide default override source for <see cref="StorageLocationProvider"/> instances created without one.
    /// A host sets it once at startup, before the first provider is created; the Windows host sets it to the registry source.
    /// </summary>
    public static class StorageOverrides
    {
        /// <summary>The source used when a provider is created without one. Defaults to <see cref="NullStorageOverrideSource"/>.</summary>
        public static IStorageOverrideSource Current { get; set; } = new NullStorageOverrideSource();
    }
}
