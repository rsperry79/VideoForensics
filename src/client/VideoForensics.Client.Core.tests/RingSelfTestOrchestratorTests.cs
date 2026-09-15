using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class RingSelfTestOrchestratorTests
    {
        private readonly Mock<ILogger<RingSelfTestOrchestrator>> _loggerMock;
        private readonly Mock<ISessionProvider> _sessionProviderMock;
        private readonly RingSelfTestOrchestrator _orchestrator;

        public RingSelfTestOrchestratorTests()
        {
            _loggerMock = new Mock<ILogger<RingSelfTestOrchestrator>>();
            _sessionProviderMock = new Mock<ISessionProvider>();
            _orchestrator = new RingSelfTestOrchestrator(
                _loggerMock.Object,
                _sessionProviderMock.Object);
        }

        [Fact]
        public void GetStatus_BeforeAnyRun_ReturnsIdle()
        {
            (SelfTestRunStatus status, DateTime? startedAtUtc, DateTime? completedAtUtc, string? error) = _orchestrator.GetStatus();

            Assert.Equal(SelfTestRunStatus.Idle, status);
            Assert.Null(startedAtUtc);
            Assert.Null(completedAtUtc);
            Assert.Null(error);
        }

        [Fact]
        public void TryStartRun_FirstCall_ReturnsTrue()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(outputDir);
            try
            {
                var options = new RunOptions(
                    RequestedKeys: new[] { "devices" },
                    Destructive: false,
                    NoPhysical: false,
                    LocationIdFilter: null,
                    DoorbotIdFilter: null,
                    ChimeIdFilter: null);

                _ = _sessionProviderMock.Setup(sp => sp.GetSession()).Returns((Session?)null);

                bool result = _orchestrator.TryStartRun(options, outputDir);
                Assert.True(result);
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    Directory.Delete(outputDir, recursive: true);
                }
            }
        }

        [Fact]
        public void TryStartRun_FirstCallTransitionsStatusToRunning()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(outputDir);
            try
            {
                var options = new RunOptions(
                    RequestedKeys: new[] { "devices" },
                    Destructive: false,
                    NoPhysical: false,
                    LocationIdFilter: null,
                    DoorbotIdFilter: null,
                    ChimeIdFilter: null);

                _ = _sessionProviderMock.Setup(sp => sp.GetSession()).Returns((Session?)null);

                // Immediately after TryStartRun returns, status should be Running
                _ = _orchestrator.TryStartRun(options, outputDir);

                (SelfTestRunStatus status, DateTime? startedAtUtc, DateTime? completedAtUtc, string? error) = _orchestrator.GetStatus();
                Assert.Equal(SelfTestRunStatus.Running, status);
                _ = Assert.NotNull(startedAtUtc);
                Assert.Null(completedAtUtc);
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    Directory.Delete(outputDir, recursive: true);
                }
            }
        }

        [Fact]
        public void TryStartRun_ConcurrentCallWhileRunning_ReturnsFalse()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(outputDir);
            try
            {
                var options = new RunOptions(
                    RequestedKeys: new[] { "devices" },
                    Destructive: false,
                    NoPhysical: false,
                    LocationIdFilter: null,
                    DoorbotIdFilter: null,
                    ChimeIdFilter: null);

                // Mock with a delay so the first run stays in Running state
                // This ensures the second call finds the status still Running
                var delaySource = new TaskCompletionSource<bool>();
                _ = _sessionProviderMock
                    .Setup(sp => sp.GetSession())
                    .Returns(() =>
                    {
                        // Simulate a delay by blocking on a tcs that never completes
                        _ = delaySource.Task.Wait(TimeSpan.FromMilliseconds(500));
                        return null;
                    });

                // First call starts the run
                bool result1 = _orchestrator.TryStartRun(options, outputDir);
                Assert.True(result1);

                // Immediately after, second call should fail (already running)
                bool result2 = _orchestrator.TryStartRun(options, outputDir);
                Assert.False(result2);
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    Directory.Delete(outputDir, recursive: true);
                }
            }
        }

        [Fact]
        public void GetResult_BeforeAnyRun_ReturnsNull()
        {
            IndexDocument? result = _orchestrator.GetResult();
            Assert.Null(result);
        }

        [Fact]
        public void GetResult_ImmediatelyAfterStart_ReturnsNull()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(outputDir);
            try
            {
                var options = new RunOptions(
                    RequestedKeys: new[] { "devices" },
                    Destructive: false,
                    NoPhysical: false,
                    LocationIdFilter: null,
                    DoorbotIdFilter: null,
                    ChimeIdFilter: null);

                _ = _sessionProviderMock.Setup(sp => sp.GetSession()).Returns((Session?)null);

                _ = _orchestrator.TryStartRun(options, outputDir);

                // Result should be null immediately after start (not completed yet)
                IndexDocument? result = _orchestrator.GetResult();
                Assert.Null(result);
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    Directory.Delete(outputDir, recursive: true);
                }
            }
        }
    }
}
