using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class StoragePathDisplay_Tests
{
    private static StorageCategoryStatus Status(StorageCategory category, string path, bool isDefault)
    {
        return new StorageCategoryStatus(
            Category: category,
            CurrentPath: path,
            IsDefault: isDefault,
            UsedBytes: 0,
            FreeBytes: 0,
            IsRelocatable: category != StorageCategory.Keys,
            RequiresRestart: false);
    }

    [Fact]
    public void Format_DefaultLocation_ReturnsDefaultSlashCategory()
    {
        StorageCategoryStatus status = Status(StorageCategory.Keys, @"C:\ProgramData\VideoForensics\Keys", isDefault: true);

        string display = StoragePathDisplay.Format(status, "Default");

        Assert.Equal("Default/Keys", display);
    }

    [Fact]
    public void Format_OverriddenLocation_ReturnsCurrentPath()
    {
        const string path = @"E:\VideoForensics";
        StorageCategoryStatus status = Status(StorageCategory.Media, path, isDefault: false);

        string display = StoragePathDisplay.Format(status, "Default");

        Assert.Equal(path, display);
    }

    [Fact]
    public void Format_DefaultLocation_UsesLocalizedPrefix()
    {
        StorageCategoryStatus status = Status(StorageCategory.Logs, @"C:\ProgramData\VideoForensics\Logs", isDefault: true);

        string display = StoragePathDisplay.Format(status, "Standard");

        Assert.Equal("Standard/Logs", display);
    }
}
