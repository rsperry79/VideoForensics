namespace VideoForensics.Hosting.Tests
{
    /// <summary>The first sync backfills forward from a year ago, one month at a time, oldest first, ending at now.</summary>
    public class SyncBackfillWindowsTests
    {
        private static readonly DateTime Now = new(2026, 10, 21, 9, 30, 0, DateTimeKind.Utc);

        [Fact]
        public void MonthlyWindows_StartsAYearAgoAndStepsAMonth()
        {
            IReadOnlyList<(DateTime From, DateTime To)> windows = AccountSyncRunner.MonthlyWindows(Now);

            Assert.Equal(new DateTime(2025, 10, 21, 9, 30, 0, DateTimeKind.Utc), windows[0].From);
            Assert.Equal(windows[0].From.AddMonths(1), windows[0].To);
            Assert.Equal(12, windows.Count);
        }
        [Fact]
        public void MonthlyWindows_SecondWindowIsElevenMonthsAgoToTenMonthsAgo()
        {
            IReadOnlyList<(DateTime From, DateTime To)> windows = AccountSyncRunner.MonthlyWindows(Now);

            Assert.Equal(Now.AddMonths(-11), windows[1].From);
            Assert.Equal(Now.AddMonths(-10), windows[1].To);
        }

        [Fact]
        public void MonthlyWindows_AreContiguousAndOldestFirst()
        {
            IReadOnlyList<(DateTime From, DateTime To)> windows = AccountSyncRunner.MonthlyWindows(Now);

            for (int i = 1; i < windows.Count; i++)
            {
                Assert.Equal(windows[i - 1].To, windows[i].From);
                Assert.True(windows[i].From > windows[i - 1].From);
            }
        }

        [Fact]
        public void MonthlyWindows_LastWindowEndsAtNow()
        {
            Assert.Equal(Now, AccountSyncRunner.MonthlyWindows(Now)[^1].To);
        }
        [Fact] public void OneMonthFloor_OlderStartIsRaisedToOneMonthBack() { Assert.Equal(Now.AddMonths(-1), AccountSyncRunner.OneMonthFloor(Now.AddMonths(-6), Now)); }
        [Fact] public void OneMonthFloor_RecentStartIsKept() { DateTime recent = Now.AddDays(-3); Assert.Equal(recent, AccountSyncRunner.OneMonthFloor(recent, Now)); }
        [Fact] public void OneMonthFloor_NoStartBecomesOneMonthBack() { Assert.Equal(Now.AddMonths(-1), AccountSyncRunner.OneMonthFloor(null, Now)); }
        [Fact] public void NextBackfillEnd_IsOneMonthOn() { DateTime start = Now.AddMonths(-11); Assert.Equal(Now.AddMonths(-10), AccountSyncRunner.NextBackfillEnd(start, Now)); }
        [Fact] public void NextBackfillEnd_StopsAtNow() { Assert.Equal(Now, AccountSyncRunner.NextBackfillEnd(Now.AddDays(-3), Now)); }
        [Fact] public void WasLimited_OlderThanAMonth_IsTrue() { Assert.True(AccountSyncRunner.WasLimited(Now.AddMonths(-3), Now)); }
        [Fact] public void WasLimited_WithinAMonthOrNoStart_IsFalse() { Assert.False(AccountSyncRunner.WasLimited(Now.AddDays(-3), Now)); Assert.False(AccountSyncRunner.WasLimited(null, Now)); }
    }
}
