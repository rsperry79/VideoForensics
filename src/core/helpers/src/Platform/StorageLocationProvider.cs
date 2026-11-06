using System;
using System.IO;

namespace VideoForensics.Core.Helpers.Platform;

public class StorageLocationProvider : IStorageLocationProvider
{
    private readonly IStorageOverrideSource _overrides;
    private readonly string? _dataRoot;

    /// <param name="overrides">
    /// Source for install-time overrides. Defaults to <see cref="StorageOverrides.Current"/>, so existing call sites
    /// pick up the host's registry source without changes.
    /// </param>
    /// <param name="dataRoot">
    /// Folder holding the legacy pointer files. Null uses the platform data root. Tests pass a temp folder.
    /// </param>
    public StorageLocationProvider(IStorageOverrideSource? overrides = null, string? dataRoot = null)
    {
        _overrides = overrides ?? StorageOverrides.Current;
        _dataRoot = dataRoot;
    }

    /// <summary>Windows registry location for install-time per-category path overrides, written by
    /// the installer's optional "Data Directory" page (deploy/windows/VideoForensics.iss) and read
    /// here so the server, the console host, and the Windows Service all resolve the same effective
    /// paths regardless of how each was launched - unlike an environment variable, a registry value
    /// needs no session/service-specific propagation trick to be visible to every process.
    ///
    /// One value per category rather than a single shared root, matching how the in-app Storage
    /// Settings feature (IForensicsConfiguration/StorageSettingsService) already treats these as
    /// independently relocatable. <see cref="StorageCategory.Keys"/> deliberately has no override -
    /// it's excluded from relocation in StorageSettingsService too, and isn't worth exposing in an
    /// installer UI given its security sensitivity.</summary>
    public const string RegistryKey = @"SOFTWARE\VideoForensics";

    public string GetDefaultRoot(StorageCategory category)
    {
        // A relocated Database wins over install-time and environment defaults. Every bootstrap reader
        // (server, service, utilities) resolves the Database through here, so they all open the moved file.
        if (category == StorageCategory.Database)
        {
            string? relocated = DatabaseLocationPointerStore.Current.Read();
            if (relocated is not null)
            {
                return relocated;
            }
        }

        // Keys are read before the saved configuration is available, so a relocated Keys folder is read from its pointer.
        if (category == StorageCategory.Keys)
        {
            // The saved Keys folder wins. Once saved settings were read, a null here means default, so a stale pointer is ignored.
            string? relocated = StorageLocationStore.KeysRoot ?? LegacyPointer(() => KeysLocationPointer.Read(GetDataRoot()));
            if (relocated is not null)
            {
                return relocated;
            }
        }

        // The logger starts before the saved configuration can be read, so a relocated Logs folder is read from its pointer.
        if (category == StorageCategory.Logs)
        {
            // The saved Logs folder wins. Once saved settings were read, a null here means default, so a stale pointer is ignored.
            string? relocated = StorageLocationStore.LogsRoot ?? LegacyPointer(() => LogsLocationPointer.Read(GetDataRoot()));
            if (relocated is not null)
            {
                return relocated;
            }
        }

        return GetPlatformDefaultRoot(category);
    }

    public string GetPlatformDefaultRoot(StorageCategory category)
    {
        if (OperatingSystem.IsWindows())
        {
            // Keys has no registry value by design, so it is never relocated, whatever the source says.
            string? configured = GetRegistryValueName(category) is null ? null : _overrides.GetOverride(category);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            string root = GetDefaultStorageRoot();
            return category switch
            {
                StorageCategory.Database => Path.Combine(root, "Database"),
                StorageCategory.Media => Path.Combine(root, "Media"),
                StorageCategory.TempDownload => Path.Combine(root, "Temp"),
                StorageCategory.Logs => Path.Combine(root, "Logs"),
                StorageCategory.Reports => Path.Combine(root, "Reports"),
                StorageCategory.Backup => Path.Combine(root, "Backup"),
                // Keys default to the default storage root. Keys already on the data root are pinned there at startup (KeysLocationPinner).
                StorageCategory.Keys => Path.Combine(root, "Keys"),
                StorageCategory.Exports => Path.Combine(root, "Exports"),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
            };
        }

        string? configuredLinux = GetConfiguredLinuxPath(category);
        string linuxRoot = GetDefaultStorageRoot();
        return configuredLinux is not null
            ? configuredLinux
            : category switch
            {
                StorageCategory.Database => Path.Combine(linuxRoot, "Database"),
                StorageCategory.Media => Path.Combine(linuxRoot, "Media"),
                StorageCategory.TempDownload => Path.Combine(linuxRoot, "Tmp"),
                // Logs keep their own system folder on Linux, unchanged by the default storage root.
                StorageCategory.Logs => "/var/log/videoforensics",
                StorageCategory.Reports => Path.Combine(linuxRoot, "Reports"),
                StorageCategory.Backup => Path.Combine(linuxRoot, "Backup"),
                StorageCategory.Keys => Path.Combine(linuxRoot, "Keys"),
                StorageCategory.Exports => Path.Combine(linuxRoot, "Exports"),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
            };
    }

    /// <inheritdoc />
    public string GetDefaultStorageRoot()
    {
        // The saved default root wins. The pointer is still read when no row is saved: the default root is where the
        // database itself lives, so it has to be found before the saved settings can be read.
        return StorageLocationStore.DefaultRoot ?? DefaultStorageRootPointer.Read(GetDataRoot()) ?? GetDataRoot();
    }

    /// <summary>
    /// Reads a legacy pointer file only when the database was not read. A read database is authoritative, so its null
    /// means the category is on its default and any leftover pointer must not move it.
    /// </summary>
    private static string? LegacyPointer(Func<string?> read)
    {
        return StorageLocationStore.SavedSettingsRead ? null : read();
    }

    /// <inheritdoc />
    public string GetDataRoot()
    {
        if (_dataRoot is not null)
        {
            return _dataRoot;
        }

        // Same root that the per-category Windows defaults hang off, so root-level files and category folders
        // always live under one VideoForensics directory.
        return OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics")
            : "/var/lib/videoforensics";
    }

    public string GetEffectiveRoot(StorageCategory category, string? configuredOverride)
    {
        return !string.IsNullOrWhiteSpace(configuredOverride) ? configuredOverride : GetDefaultRoot(category);
    }

    /// <summary>The registry value name for a category's path override, or null for categories that
    /// deliberately have no override (Keys).</summary>
    public static string? GetRegistryValueName(StorageCategory category)
    {
        return category switch
        {
            StorageCategory.Database => "DatabasePath",
            StorageCategory.Media => "MediaPath",
            StorageCategory.TempDownload => "TempDownloadPath",
            StorageCategory.Logs => "LogsPath",
            StorageCategory.Reports => "ReportsPath",
            StorageCategory.Backup => "BackupPath",
            StorageCategory.Exports => "ExportsPath",
            StorageCategory.Keys => null,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
    }

    /// <summary>The environment variable name for a category's path override on Linux, or null for
    /// categories that deliberately have no override (Keys).</summary>
    public static string? GetEnvironmentVariableName(StorageCategory category)
    {
        return category switch
        {
            StorageCategory.Database => "VIDEOFORENSICS_DATABASE_PATH",
            StorageCategory.Media => "VIDEOFORENSICS_MEDIA_PATH",
            StorageCategory.TempDownload => "VIDEOFORENSICS_TEMPDOWNLOAD_PATH",
            StorageCategory.Logs => "VIDEOFORENSICS_LOGS_PATH",
            StorageCategory.Reports => "VIDEOFORENSICS_REPORTS_PATH",
            StorageCategory.Backup => "VIDEOFORENSICS_BACKUP_PATH",
            StorageCategory.Exports => "VIDEOFORENSICS_EXPORTS_PATH",
            StorageCategory.Keys => null,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
    }

    private static string? GetConfiguredLinuxPath(StorageCategory category)
    {
        string? envVarName = GetEnvironmentVariableName(category);
        if (envVarName is null)
        {
            return null;
        }

        string? value = Environment.GetEnvironmentVariable(envVarName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
