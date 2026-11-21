using VideoForensics.Client.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    /// <summary>Gap planner: which time-lapse segments a device needs between its events, capped by the max segment length.</summary>
    public class GapPlannerTests
    {
        private static readonly DateTime Day = new(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Max = TimeSpan.FromMinutes(60);

        private static DateTime At(int hours, int minutes = 0) => Day.AddHours(hours).AddMinutes(minutes);

        private static IReadOnlyList<DateTime> Every(TimeSpan step, DateTime from, DateTime to)
        {
            var list = new List<DateTime>();
            for (DateTime t = from; t < to; t += step)
            {
                list.Add(t);
            }

            return list;
        }

        [Fact]
        public void Plan_FortyThreeMinuteGap_OneSegmentOfFortyThreeMinutes()
        {
            var snapshots = new[] { At(10), At(10, 20), At(10, 40) };

            GapPlanResult result = GapPlanner.Plan(At(10), At(11), [At(10, 43)], snapshots, Max);

            GapSegment segment = Assert.Single(result.Segments);
            Assert.Equal(At(10), segment.StartUtc);
            Assert.Equal(At(10, 43), segment.EndUtc);
            Assert.Equal(At(10, 43), result.NextCursorUtc);
        }

        [Fact]
        public void Plan_EightAndAHalfHourGap_EightHourSegmentsThenThirtyMinutes()
        {
            IReadOnlyList<DateTime> snapshots = Every(TimeSpan.FromMinutes(10), At(0), At(8, 30));

            GapPlanResult result = GapPlanner.Plan(At(0), At(9), [At(8, 30)], snapshots, Max);

            Assert.Equal(9, result.Segments.Count);
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(TimeSpan.FromMinutes(60), result.Segments[i].EndUtc - result.Segments[i].StartUtc);
            }

            Assert.Equal(TimeSpan.FromMinutes(30), result.Segments[8].EndUtc - result.Segments[8].StartUtc);
            Assert.Equal(At(8, 30), result.NextCursorUtc);
        }

        [Fact]
        public void Plan_OpenGapNoClosingEvent_FillsFullSegmentsOnlyAndLeavesRemainder()
        {
            IReadOnlyList<DateTime> snapshots = Every(TimeSpan.FromMinutes(30), At(0), At(2, 30));

            GapPlanResult result = GapPlanner.Plan(At(0), At(2, 30), [], snapshots, Max);

            Assert.Equal(2, result.Segments.Count);
            Assert.Equal(At(0), result.Segments[0].StartUtc);
            Assert.Equal(At(1), result.Segments[0].EndUtc);
            Assert.Equal(At(1), result.Segments[1].StartUtc);
            Assert.Equal(At(2), result.Segments[1].EndUtc);
            Assert.Equal(At(2), result.NextCursorUtc);
        }

        [Fact]
        public void Plan_ShortGapWithOneSnapshot_WritesNothingButAdvancesCursor()
        {
            var snapshots = new[] { At(10) };

            GapPlanResult result = GapPlanner.Plan(At(10), At(11), [At(10, 43)], snapshots, Max);

            Assert.Empty(result.Segments);
            Assert.Equal(At(10, 43), result.NextCursorUtc);
        }

        [Fact]
        public void Plan_BackToBackEvents_OneSegmentPerGapAndNoOverlap()
        {
            IReadOnlyList<DateTime> snapshots = Every(TimeSpan.FromSeconds(20), At(10), At(10, 3));

            GapPlanResult result = GapPlanner.Plan(At(10), At(11), [At(10, 1), At(10, 2)], snapshots, Max);

            Assert.Equal(At(10, 1), result.Segments[0].EndUtc);
            foreach (GapSegment s in result.Segments)
            {
                Assert.True(s.EndUtc > s.StartUtc);
            }

            for (int i = 1; i < result.Segments.Count; i++)
            {
                Assert.True(result.Segments[i].StartUtc >= result.Segments[i - 1].EndUtc);
            }
        }

        [Fact]
        public void Plan_GapExactlyTwoMaxSegments_TwoFullSegmentsNoRemainder()
        {
            IReadOnlyList<DateTime> snapshots = Every(TimeSpan.FromMinutes(30), At(0), At(2));

            GapPlanResult result = GapPlanner.Plan(At(0), At(3), [At(2)], snapshots, Max);

            Assert.Equal(2, result.Segments.Count);
            Assert.Equal(At(2), result.Segments[1].EndUtc);
        }

        [Fact]
        public void Plan_UnsortedAndDuplicateEvents_TreatedAsOrderedDistinctBoundaries()
        {
            IReadOnlyList<DateTime> snapshots = Every(TimeSpan.FromMinutes(10), At(10), At(11));

            GapPlanResult result = GapPlanner.Plan(At(10), At(11), [At(10, 40), At(10, 20), At(10, 40)], snapshots, Max);

            Assert.Equal(At(10, 20), result.Segments[0].EndUtc);
            Assert.Equal(At(10, 40), result.Segments[1].EndUtc);
            Assert.Equal(2, result.Segments.Count);
        }

        [Fact]
        public void Plan_EventAfterHorizon_IgnoredAndNothingPlannedPastHorizon()
        {
            IReadOnlyList<DateTime> snapshots = Every(TimeSpan.FromMinutes(10), At(10), At(12));

            GapPlanResult result = GapPlanner.Plan(At(10), At(11), [At(11, 30)], snapshots, Max);

            Assert.All(result.Segments, s => Assert.True(s.EndUtc <= At(11)));
            Assert.True(result.NextCursorUtc <= At(11));
        }

        [Fact]
        public void Plan_CursorAtOrPastHorizon_NoSegments()
        {
            IReadOnlyList<DateTime> snapshots = Every(TimeSpan.FromMinutes(10), At(10), At(12));

            GapPlanResult result = GapPlanner.Plan(At(11), At(11), [], snapshots, Max);

            Assert.Empty(result.Segments);
            Assert.Equal(At(11), result.NextCursorUtc);
        }
    }
}
