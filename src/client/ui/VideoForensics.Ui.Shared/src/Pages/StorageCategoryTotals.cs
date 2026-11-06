using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Ui.Shared.Pages
{
    /// <summary>Orders and totals the category rows shown on the Storage page.</summary>
    public static class StorageCategoryTotals
    {
        /// <summary>Sorts the rows by category name, so the table reads in a fixed order regardless of the server's order.</summary>
        public static IReadOnlyList<StorageCategoryStatus> Ordered(IEnumerable<StorageCategoryStatus> rows)
        {
            return rows.OrderBy(r => r.Category.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The bytes used across every category shown in the table.</summary>
        public static long TotalUsed(IEnumerable<StorageCategoryStatus> rows)
        {
            return rows.Sum(r => r.UsedBytes);
        }
    }
}
