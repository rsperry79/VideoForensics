using System;
using System.IO;

namespace VideoForensics.Providers.Common.Helpers.Platform;

public class StorageLocationProvider : IStorageLocationProvider
{
    public string GetDefaultRoot(StorageCategory category)
    {
        if (OperatingSystem.IsWindows())
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "VideoForensics");

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
}
