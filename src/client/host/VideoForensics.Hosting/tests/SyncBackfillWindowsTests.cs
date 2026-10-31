namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>The first sync backfills forward from a year ago, one week at a time, oldest first, ending at now. No pull reaches back more than a week.</summary>
    public class SyncBackfillWindowsTests
    {
        private static readonly DateTime Now = new(2026, 10, 21, 9, 30, 0, DateTimeKind.Utc);

        [Fact]
        public void WeeklyWindows_StartsAYearAgoAndStepsAWeek()
        {
            IReadOnlyList<(DateTime From, DateTime To)> windows = AccountSyncRunner.WeeklyWindows(Now);

            Assert.Equal(new DateTime(2025, 10, 21, 9, 30, 0, DateTimeKind.Utc), windows[0].From);
            Assert.Equal(windows[0].From.AddDays(7), windows[0].To);
            Assert.Equal(53, windows.Count);
        }

        [Fact]
        public void WeeklyWindows_SecondWindowIsSevenDaysAfterTheYearStart()
        {
            IReadOnlyList<(DateTime From, DateTime To)> windows = AccountSyncRunner.WeeklyWindows(Now);

            Assert.Equal(Now.AddYears(-1).AddDays(7), windows[1].From);
            Assert.Equal(Now.AddYears(-1).AddDays(14), windows[1].To);
        }

        [Fact]
        public void WeeklyWindows_AreContiguousAndOldestFirst()
        {
            IReadOnlyList<(DateTime From, DateTime To)> windows = AccountSyncRunner.WeeklyWindows(Now);

            for (int i = 1; i < windows.Count; i++)
            {
                Assert.Equal(windows[i - 1].To, windows[i].From);
                Assert.True(windows[i].From > windows[i - 1].From);
            }
        }

        [Fact]
        public void WeeklyWindows_LastWindowEndsAtNow()
        {
            Assert.Equal(Now, AccountSyncRunner.WeeklyWindows(Now)[^1].To);
        }

        [Fact] public void OneWeekFloor_OlderStartIsRaisedToOneWeekBack() { Assert.Equal(Now.AddDays(-7), AccountSyncRunner.OneWeekFloor(Now.AddMonths(-6), Now)); }
        [Fact] public void OneWeekFloor_RecentStartIsKept() { DateTime recent = Now.AddDays(-3); Assert.Equal(recent, AccountSyncRunner.OneWeekFloor(recent, Now)); }
        [Fact] public void OneWeekFloor_NoStartBecomesOneWeekBack() { Assert.Equal(Now.AddDays(-7), AccountSyncRunner.OneWeekFloor(null, Now)); }
        [Fact] public void NextBackfillEnd_IsOneWeekOn() { DateTime start = Now.AddDays(-14); Assert.Equal(Now.AddDays(-7), AccountSyncRunner.NextBackfillEnd(start, Now)); }
        [Fact] public void NextBackfillEnd_StopsAtNow() { Assert.Equal(Now, AccountSyncRunner.NextBackfillEnd(Now.AddDays(-3), Now)); }
        [Fact] public void WasLimited_OlderThanAWeek_IsTrue() { Assert.True(AccountSyncRunner.WasLimited(Now.AddDays(-10), Now)); }
        [Fact] public void WasLimited_WithinAWeekOrNoStart_IsFalse() { Assert.False(AccountSyncRunner.WasLimited(Now.AddDays(-3), Now)); Assert.False(AccountSyncRunner.WasLimited(null, Now)); }
    }
}
