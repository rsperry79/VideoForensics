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
            var (status, startedAtUtc, completedAtUtc, error) = _orchestrator.GetStatus();

            Assert.Equal(SelfTestRunStatus.Idle, status);
            Assert.Null(startedAtUtc);
            Assert.Null(completedAtUtc);
            Assert.Null(error);
        }

        [Fact]
        public void TryStartRun_FirstCall_ReturnsTrue()
        {
            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
            try
            {
                var options = new RunOptions(
                    RequestedKeys: new[] { "devices" },
                    Destructive: false,
                    NoPhysical: false,
                    LocationIdFilter: null,
                    DoorbotIdFilter: null,
                    ChimeIdFilter: null);

                _sessionProviderMock.Setup(sp => sp.GetSession()).Returns((Session?)null);

                var result = _orchestrator.TryStartRun(options, outputDir);
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
            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
            try
            {
                var options = new RunOptions(
                    RequestedKeys: new[] { "devices" },
                    Destructive: false,
                    NoPhysical: false,
                    LocationIdFilter: null,
                    DoorbotIdFilter: null,
                    ChimeIdFilter: null);

                _sessionProviderMock.Setup(sp => sp.GetSession()).Returns((Session?)null);

                // Immediately after TryStartRun returns, status should be Running
                _orchestrator.TryStartRun(options, outputDir);

                var (status, startedAtUtc, completedAtUtc, error) = _orchestrator.GetStatus();
                Assert.Equal(SelfTestRunStatus.Running, status);
                Assert.NotNull(startedAtUtc);
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
            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
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
                _sessionProviderMock
                    .Setup(sp => sp.GetSession())
                    .Returns(() =>
                    {
                        // Simulate a delay by blocking on a tcs that never completes
                        delaySource.Task.Wait(TimeSpan.FromMilliseconds(500));
                        return null;
                    });

                // First call starts the run
                var result1 = _orchestrator.TryStartRun(options, outputDir);
                Assert.True(result1);

                // Immediately after, second call should fail (already running)
                var result2 = _orchestrator.TryStartRun(options, outputDir);
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
            var result = _orchestrator.GetResult();
            Assert.Null(result);
        }

        [Fact]
        public void GetResult_ImmediatelyAfterStart_ReturnsNull()
        {
            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
            try
            {
                var options = new RunOptions(
                    RequestedKeys: new[] { "devices" },
                    Destructive: false,
                    NoPhysical: false,
                    LocationIdFilter: null,
                    DoorbotIdFilter: null,
                    ChimeIdFilter: null);

                _sessionProviderMock.Setup(sp => sp.GetSession()).Returns((Session?)null);

                _orchestrator.TryStartRun(options, outputDir);

                // Result should be null immediately after start (not completed yet)
                var result = _orchestrator.GetResult();
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
