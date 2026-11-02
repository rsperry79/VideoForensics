namespace VideoForensics.Core.Helpers.Platform;

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
    /// Gets the platform default root for the specified category, ignoring a relocated Database pointer.
    /// Used when a category is reset to its default.
    /// </summary>
    /// <param name="category">The storage category.</param>
    /// <returns>An OS-appropriate absolute folder path.</returns>
    string GetPlatformDefaultRoot(StorageCategory category);

    /// <summary>
    /// Gets the default storage root that every category's platform default is built from: the root saved by the
    /// default storage path setting, or the built-in data root when none is saved.
    /// </summary>
    /// <returns>An OS-appropriate absolute folder path.</returns>
    string GetDefaultStorageRoot();

    /// <summary>
    /// Gets the machine-wide data root that holds the root-level app files (license key, credential salt,
    /// cached server URL) and the per-category folders. Windows: %ProgramData%\VideoForensics. Linux: /var/lib/videoforensics.
    /// Ignores install-time and in-app per-category overrides, which apply only to <see cref="GetDefaultRoot"/>.
    /// </summary>
    /// <returns>An OS-appropriate absolute folder path.</returns>
    string GetDataRoot();

    /// <summary>
    /// Gets the effective root folder path for the specified storage category, applying user override if configured.
    /// </summary>
    /// <param name="category">The storage category.</param>
    /// <param name="configuredOverride">User-configured override path, or null/whitespace to use the default.</param>
    /// <returns>The configured override path if non-null and non-whitespace; otherwise, the default root path.</returns>
    string GetEffectiveRoot(StorageCategory category, string? configuredOverride);
}
