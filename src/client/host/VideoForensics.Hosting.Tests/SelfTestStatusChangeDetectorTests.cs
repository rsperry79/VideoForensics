using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class SelfTestStatusChangeDetectorTests
    {
        private static readonly DateTime Started = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Completed = new(2026, 10, 1, 12, 0, 30, DateTimeKind.Utc);

        private static SelfTestStatusDto Build(
            SelfTestRunStatus status = SelfTestRunStatus.Running,
            DateTime? startedAtUtc = null,
            DateTime? completedAtUtc = null,
            string? error = null)
        {
            return new SelfTestStatusDto(status, startedAtUtc ?? Started, completedAtUtc, error);
        }

        [Fact]
        public void SelfTestStatusChangeDetector_IdenticalStatus_NotChanged()
        {
            ISelfTestStatusChangeDetector detector = new SelfTestStatusChangeDetector();

            Assert.False(detector.HasChanged(Build(), Build()));
        }

        [Fact]
        public void SelfTestStatusChangeDetector_NoPreviousSnapshot_Changed()
        {
            ISelfTestStatusChangeDetector detector = new SelfTestStatusChangeDetector();

            Assert.True(detector.HasChanged(null, Build()));
        }

        [Fact]
        public void SelfTestStatusChangeDetector_StatusDiffers_Changed()
        {
            ISelfTestStatusChangeDetector detector = new SelfTestStatusChangeDetector();

            Assert.True(detector.HasChanged(
                Build(SelfTestRunStatus.Running),
                Build(SelfTestRunStatus.Completed, completedAtUtc: Completed)));
        }

        [Fact]
        public void SelfTestStatusChangeDetector_StartedAtDiffers_Changed()
        {
            ISelfTestStatusChangeDetector detector = new SelfTestStatusChangeDetector();

            Assert.True(detector.HasChanged(
                Build(startedAtUtc: Started),
                Build(startedAtUtc: Started.AddSeconds(1))));
        }

        [Fact]
        public void SelfTestStatusChangeDetector_CompletedAtDiffers_Changed()
        {
            ISelfTestStatusChangeDetector detector = new SelfTestStatusChangeDetector();

            Assert.True(detector.HasChanged(
                Build(SelfTestRunStatus.Completed, completedAtUtc: Completed),
                Build(SelfTestRunStatus.Completed, completedAtUtc: Completed.AddSeconds(1))));
        }

        [Fact]
        public void SelfTestStatusChangeDetector_ErrorDiffers_Changed()
        {
            ISelfTestStatusChangeDetector detector = new SelfTestStatusChangeDetector();

            Assert.True(detector.HasChanged(
                Build(SelfTestRunStatus.Failed, completedAtUtc: Completed, error: null),
                Build(SelfTestRunStatus.Failed, completedAtUtc: Completed, error: "boom")));
        }

        [Fact]
        public void SelfTestStatusChangeDetector_SameValuesNewInstance_NotChanged()
        {
            ISelfTestStatusChangeDetector detector = new SelfTestStatusChangeDetector();
            SelfTestStatusDto previous = new(SelfTestRunStatus.Failed, Started, Completed, "boom");
            SelfTestStatusDto current = new(SelfTestRunStatus.Failed, Started, Completed, "boom");

            Assert.False(detector.HasChanged(previous, current));
        }
    }
}
