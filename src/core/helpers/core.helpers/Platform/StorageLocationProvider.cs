using System;
using System.IO;

namespace VideoForensics.Providers.Common.Helpers.Platform;

public class StorageLocationProvider : IStorageLocationProvider
{
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
        if (OperatingSystem.IsWindows())
        {
            string? configured = GetConfiguredWindowsPath(category);
            if (configured is not null)
            {
                return configured;
            }

            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics");
            return category switch
            {
                StorageCategory.Database => root,
                StorageCategory.Media => Path.Combine(root, "media"),
                StorageCategory.TempDownload => Path.Combine(root, "temp"),
                StorageCategory.Logs => Path.Combine(root, "Logs"),
                StorageCategory.Reports => Path.Combine(root, "Reports"),
                StorageCategory.Backup => Path.Combine(root, "backup"),
                StorageCategory.Keys => Path.Combine(root, "keys"),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
            };
        }

        string? configuredLinux = GetConfiguredLinuxPath(category);
        if (configuredLinux is not null)
        {
            return configuredLinux;
        }

        return category switch
        {
            StorageCategory.Database => "/var/lib/videoforensics",
            StorageCategory.Media => "/var/lib/videoforensics/media",
            StorageCategory.TempDownload => "/var/lib/videoforensics/tmp",
            StorageCategory.Logs => "/var/log/videoforensics",
            StorageCategory.Reports => "/var/lib/videoforensics/reports",
            StorageCategory.Backup => "/var/lib/videoforensics/backup",
            StorageCategory.Keys => "/var/lib/videoforensics/keys",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
    }

    public string GetEffectiveRoot(StorageCategory category, string? configuredOverride)
    {
        return !string.IsNullOrWhiteSpace(configuredOverride) ? configuredOverride : GetDefaultRoot(category);
    }

    /// <summary>The registry value name for a category's path override, or null for categories that
    /// deliberately have no override (Keys).</summary>
    public static string? GetRegistryValueName(StorageCategory category) => category switch
    {
        StorageCategory.Database => "DatabasePath",
        StorageCategory.Media => "MediaPath",
        StorageCategory.TempDownload => "TempDownloadPath",
        StorageCategory.Logs => "LogsPath",
        StorageCategory.Reports => "ReportsPath",
        StorageCategory.Backup => "BackupPath",
        StorageCategory.Keys => null,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>The environment variable name for a category's path override on Linux, or null for
    /// categories that deliberately have no override (Keys).</summary>
    public static string? GetEnvironmentVariableName(StorageCategory category) => category switch
    {
        StorageCategory.Database => "VIDEOFORENSICS_DATABASE_PATH",
        StorageCategory.Media => "VIDEOFORENSICS_MEDIA_PATH",
        StorageCategory.TempDownload => "VIDEOFORENSICS_TEMPDOWNLOAD_PATH",
        StorageCategory.Logs => "VIDEOFORENSICS_LOGS_PATH",
        StorageCategory.Reports => "VIDEOFORENSICS_REPORTS_PATH",
        StorageCategory.Backup => "VIDEOFORENSICS_BACKUP_PATH",
        StorageCategory.Keys => null,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    private static string? GetConfiguredWindowsPath(StorageCategory category)
    {
        string? valueName = GetRegistryValueName(category);
        if (valueName is null)
        {
            return null;
        }

        try
        {
            using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RegistryKey);
            if (key?.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        catch
        {
            // Registry read failed (permissions, key missing, etc.) - fall back to the default path.
        }

        return null;
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
