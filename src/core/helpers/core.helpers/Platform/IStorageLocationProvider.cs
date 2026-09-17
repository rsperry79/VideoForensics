namespace VideoForensics.Providers.Common.Helpers.Platform;

/// <summary>
/// Provides OS-appropriate storage location paths for different storage categories.
/// </summary>
public interface IStorageLocationProvider
{
    /// <summary>
    /// Gets the default root folder path for the specified storage category, without considering any user override.
    /// </summary>
    /// <param name="category">The storage category.</param>
    /// <returns>An OS-appropriate absolute folder path.</returns>
    string GetDefaultRoot(StorageCategory category);

    /// <summary>
    /// Gets the effective root folder path for the specified storage category, applying user override if configured.
    /// </summary>
    /// <param name="category">The storage category.</param>
    /// <param name="configuredOverride">User-configured override path, or null/whitespace to use the default.</param>
    /// <returns>The configured override path if non-null and non-whitespace; otherwise, the default root path.</returns>
    string GetEffectiveRoot(StorageCategory category, string? configuredOverride);
}
