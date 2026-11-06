using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class StorageCategoryTotals_Tests
{
    private static StorageCategoryStatus Status(StorageCategory category, long usedBytes)
    {
        return new StorageCategoryStatus(
            Category: category,
            CurrentPath: "x",
            IsDefault: true,
            UsedBytes: usedBytes,
            FreeBytes: 0,
            IsRelocatable: category != StorageCategory.Keys,
            RequiresRestart: false);
    }

    [Fact]
    public void Ordered_SortsByCategoryNameAlphabetically()
    {
        StorageCategoryStatus[] rows = new[]
        {
            Status(StorageCategory.Logs, 0),
            Status(StorageCategory.Database, 0),
            Status(StorageCategory.TempDownload, 0),
            Status(StorageCategory.Media, 0),
        };

        var ordered = StorageCategoryTotals.Ordered(rows).Select(r => r.Category).ToList();

        Assert.Equal(new[] { StorageCategory.Database, StorageCategory.Logs, StorageCategory.Media, StorageCategory.TempDownload }, ordered);
    }

    [Fact]
    public void TotalUsed_SumsUsedBytesAcrossCategories()
    {
        StorageCategoryStatus[] rows = new[]
        {
            Status(StorageCategory.Database, 100),
            Status(StorageCategory.Media, 250),
            Status(StorageCategory.Logs, 50),
        };

        Assert.Equal(400L, StorageCategoryTotals.TotalUsed(rows));
    }

    [Fact]
    public void TotalUsed_NoCategories_IsZero()
    {
        Assert.Equal(0L, StorageCategoryTotals.TotalUsed(Array.Empty<StorageCategoryStatus>()));
    }
}
