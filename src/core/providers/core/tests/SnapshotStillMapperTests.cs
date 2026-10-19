using VideoForensics.Core.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class SnapshotStillMapperTests : IDisposable
    {
        private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly string _root = Path.Combine(Path.GetTempPath(), "snapshot-still-tests-" + Guid.NewGuid().ToString("N"));
        private readonly Guid _device = Guid.NewGuid();
        private readonly SnapshotStillMapper _mapper = new();

        public SnapshotStillMapperTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the test directory.
            }
        }

        [Fact]
        public void MapCurrentStills_Status200WithFile_BecomesStillAtCapturedTime()
        {
            WriteMedia("a.jpg");
            SnapshotStillMapRequest request = Request(new[] { Record(_device, T0, 200, "a.jpg") }, TimeSpan.FromSeconds(3));

            IReadOnlyList<TimelineStill> stills = _mapper.MapCurrentStills(request);

            TimelineStill still = Assert.Single(stills);
            Assert.Equal(Path.GetFullPath(Path.Combine(_root, "a.jpg")), still.ImagePath);
            Assert.Equal(T0, still.CapturedAt);
            Assert.Equal(TimeSpan.FromSeconds(3), still.Hold);
        }

        [Fact]
        public void MapCurrentStills_NonStatus200WithFile_IsSkipped()
        {
            WriteMedia("a.jpg");
            SnapshotStillMapRequest request = Request(new[] { Record(_device, T0, 503, "a.jpg") }, TimeSpan.FromSeconds(3));

            Assert.Empty(_mapper.MapCurrentStills(request));
        }

        [Fact]
        public void MapCurrentStills_Status200MissingFile_IsSkipped()
        {
            SnapshotStillMapRequest request = Request(new[] { Record(_device, T0, 200, "missing.jpg") }, TimeSpan.FromSeconds(3));

            Assert.Empty(_mapper.MapCurrentStills(request));
        }

        [Fact]
        public void MapCurrentStills_RecordForAnotherDevice_IsExcluded()
        {
            WriteMedia("a.jpg");
            SnapshotStillMapRequest request = Request(new[] { Record(Guid.NewGuid(), T0, 200, "a.jpg") }, TimeSpan.FromSeconds(3));

            Assert.Empty(_mapper.MapCurrentStills(request));
        }

        [Fact]
        public void MapCurrentStills_OutOfOrderRecords_SortsByCapturedAt()
        {
            WriteMedia("a.jpg");
            WriteMedia("b.jpg");
            SnapshotStillMapRequest request = Request(
                new[]
                {
                    Record(_device, T0.AddSeconds(5), 200, "b.jpg"),
                    Record(_device, T0, 200, "a.jpg"),
                },
                TimeSpan.FromSeconds(4));

            IReadOnlyList<TimelineStill> stills = _mapper.MapCurrentStills(request);

            Assert.Equal(2, stills.Count);
            Assert.Equal(T0, stills[0].CapturedAt);
            Assert.Equal(T0.AddSeconds(5), stills[1].CapturedAt);
        }

        [Fact]
        public void MapCurrentStills_StillFollowedByAnother_IsHeldUntilTheNextStill()
        {
            WriteMedia("a.jpg");
            WriteMedia("b.jpg");
            SnapshotStillMapRequest request = Request(
                new[]
                {
                    Record(_device, T0, 200, "a.jpg"),
                    Record(_device, T0.AddSeconds(10), 200, "b.jpg"),
                },
                TimeSpan.FromSeconds(5));

            IReadOnlyList<TimelineStill> stills = _mapper.MapCurrentStills(request);

            Assert.Equal(TimeSpan.FromSeconds(10), stills[0].Hold);
            Assert.Equal(TimeSpan.FromSeconds(5), stills[1].Hold);
        }

        [Fact]
        public void MapCurrentStills_FailedAttemptBetweenStills_EndsPreviousStillAtFailure()
        {
            WriteMedia("a.jpg");
            WriteMedia("b.jpg");
            SnapshotStillMapRequest request = Request(
                new[]
                {
                    Record(_device, T0, 200, "a.jpg"),
                    Record(_device, T0.AddSeconds(5), 500, null),
                    Record(_device, T0.AddSeconds(10), 200, "b.jpg"),
                },
                TimeSpan.FromSeconds(5));

            IReadOnlyList<TimelineStill> stills = _mapper.MapCurrentStills(request);

            Assert.Equal(2, stills.Count);
            Assert.Equal(TimeSpan.FromSeconds(5), stills[0].Hold);
        }

        [Fact]
        public void MapCurrentStills_Status200AfterFailedAttempt_StartsANewStill()
        {
            WriteMedia("a.jpg");
            WriteMedia("b.jpg");
            SnapshotStillMapRequest request = Request(
                new[]
                {
                    Record(_device, T0, 200, "a.jpg"),
                    Record(_device, T0.AddSeconds(5), 503, null),
                    Record(_device, T0.AddSeconds(10), 200, "b.jpg"),
                    Record(_device, T0.AddSeconds(12), 200, "b.jpg"),
                },
                TimeSpan.FromSeconds(5));

            IReadOnlyList<TimelineStill> stills = _mapper.MapCurrentStills(request);

            Assert.Equal(3, stills.Count);
            Assert.Equal(T0.AddSeconds(10), stills[1].CapturedAt);
            Assert.Equal(TimeSpan.FromSeconds(2), stills[1].Hold);
        }

        [Fact]
        public void MapCurrentStills_FailedAttemptAfterLastStill_EndsItsHold()
        {
            WriteMedia("a.jpg");
            SnapshotStillMapRequest request = Request(
                new[]
                {
                    Record(_device, T0, 200, "a.jpg"),
                    Record(_device, T0.AddSeconds(7), 502, null),
                },
                TimeSpan.FromSeconds(60));

            TimelineStill still = Assert.Single(_mapper.MapCurrentStills(request));

            Assert.Equal(TimeSpan.FromSeconds(7), still.Hold);
        }

        [Fact]
        public void MapCurrentStills_FailedAttemptForAnotherDevice_DoesNotEndHold()
        {
            WriteMedia("a.jpg");
            SnapshotStillMapRequest request = Request(
                new[]
                {
                    Record(_device, T0, 200, "a.jpg"),
                    Record(Guid.NewGuid(), T0.AddSeconds(3), 500, null),
                },
                TimeSpan.FromSeconds(9));

            TimelineStill still = Assert.Single(_mapper.MapCurrentStills(request));

            Assert.Equal(TimeSpan.FromSeconds(9), still.Hold);
        }

        [Fact]
        public void MapCurrentStills_TwoCapturesAtTheSameInstant_KeepsOnlyTheLaterOne()
        {
            WriteMedia("a.jpg");
            WriteMedia("b.jpg");
            SnapshotStillMapRequest request = Request(
                new[]
                {
                    Record(_device, T0, 200, "a.jpg"),
                    Record(_device, T0, 200, "b.jpg"),
                },
                TimeSpan.FromSeconds(4));

            IReadOnlyList<TimelineStill> stills = _mapper.MapCurrentStills(request);

            TimelineStill still = Assert.Single(stills);
            Assert.Equal(Path.GetFullPath(Path.Combine(_root, "b.jpg")), still.ImagePath);
            Assert.Equal(TimeSpan.FromSeconds(4), still.Hold);
        }

        [Fact]
        public void MapCurrentStills_NonPositiveLastStillHold_ThrowsArgumentException()
        {
            SnapshotStillMapRequest request = Request(Array.Empty<SnapshotStillRecord>(), TimeSpan.Zero);

            Assert.Throws<ArgumentException>(() => _mapper.MapCurrentStills(request));
        }

        private void WriteMedia(string relative)
        {
            File.WriteAllBytes(Path.Combine(_root, relative), new byte[] { 0xFF, 0xD8, 0xFF });
        }

        private SnapshotStillMapRequest Request(IReadOnlyList<SnapshotStillRecord> records, TimeSpan lastHold) =>
            new(records, _device, _root, lastHold);

        private static SnapshotStillRecord Record(Guid device, DateTimeOffset capturedAt, int status, string? media) =>
            new(device, capturedAt, status, media);
    }
}
