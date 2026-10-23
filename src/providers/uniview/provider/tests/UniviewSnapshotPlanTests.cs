using VideoForensics.Providers.Uniview.Services;

using Xunit;

namespace VideoForensics.Providers.Uniview.Tests
{
    /// <summary>Tests for the windowed-snapshot planning logic (recorded footage, not live frame).</summary>
    public class UniviewSnapshotPlanTests
    {
        private static readonly DateTimeOffset T0 = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

        private static RecordSegment Seg(DateTimeOffset begin, int minutes) =>
            new(1, begin, begin.AddMinutes(minutes), 0);

        [Fact]
        public void UniviewMediaDownloadService_PlanSnapshotTargets_SegmentInsideWindow_UsesZeroOffset()
        {
            var segs = new[] { Seg(T0.AddMinutes(5), 10) };

            var plan = UniviewMediaDownloadService.PlanSnapshotTargets(segs, T0, T0.AddHours(1));

            var target = Assert.Single(plan);
            Assert.Equal(TimeSpan.Zero, target.Offset);
            Assert.Equal(segs[0], target.Segment);
        }

        [Fact]
        public void UniviewMediaDownloadService_PlanSnapshotTargets_SegmentStartsBeforeWindow_OffsetsToWindowStart()
        {
            var segs = new[] { Seg(T0.AddMinutes(-10), 30) };

            var plan = UniviewMediaDownloadService.PlanSnapshotTargets(segs, T0, T0.AddHours(1));

            var target = Assert.Single(plan);
            Assert.Equal(TimeSpan.FromMinutes(10), target.Offset);
        }

        [Fact]
        public void UniviewMediaDownloadService_PlanSnapshotTargets_SegmentOutsideWindow_IsSkipped()
        {
            var segs = new[] { Seg(T0.AddHours(-3), 10), Seg(T0.AddHours(3), 10) };

            var plan = UniviewMediaDownloadService.PlanSnapshotTargets(segs, T0, T0.AddHours(1));

            Assert.Empty(plan);
        }

        [Fact]
        public void UniviewMediaDownloadService_PlanSnapshotTargets_UnorderedInput_ReturnsChronological()
        {
            var late = Seg(T0.AddMinutes(40), 5);
            var early = Seg(T0.AddMinutes(1), 5);

            var plan = UniviewMediaDownloadService.PlanSnapshotTargets(new[] { late, early }, T0, T0.AddHours(1));

            Assert.Equal(new[] { early, late }, plan.Select(p => p.Segment).ToArray());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UniviewMediaDownloadService_HasRequestedWindow_DefaultOrInvertedRange_ReturnsFalse(bool inverted)
        {
            DateTime start = inverted ? new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc) : default;
            DateTime end = inverted ? new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) : default;

            Assert.False(UniviewMediaDownloadService.HasRequestedWindow(start, end));
        }

        [Fact]
        public void UniviewMediaDownloadService_HasRequestedWindow_ValidRange_ReturnsTrue()
        {
            Assert.True(UniviewMediaDownloadService.HasRequestedWindow(
                new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc)));
        }
    }
}
