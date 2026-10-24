using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class DevWebAppLauncherTests
    {
        private const string ScriptPath = @"C:\repo\deploy\windows\hotswap-service.ps1";

        [Fact]
        public async Task EnsureReady_RunsScriptEvenWhenServerAlreadyHealthy()
        {
            // The swap must run on every launch so new binaries reach the service; a healthy probe must not skip it.
            var runner = new FakeRunner(exitCode: 0);
            var probe = new FakeProbe(healthy: true);
            var launcher = new DevWebAppLauncher(probe, runner, readyTimeout: TimeSpan.FromSeconds(1), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.Ready, result.Status);
            Assert.Equal(1, runner.Calls);
            Assert.Equal(ScriptPath, runner.LastScriptPath);
        }

        [Fact]
        public async Task EnsureReady_ReportsNotRun_WhenElevationDeclined()
        {
            var runner = new FakeRunner(exitCode: null);
            var probe = new FakeProbe(healthy: true);
            var launcher = new DevWebAppLauncher(probe, runner, readyTimeout: TimeSpan.FromSeconds(1), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.ScriptNotRun, result.Status);
            Assert.Equal(0, probe.Calls);
        }

        [Fact]
        public async Task EnsureReady_ReportsScriptFailed_WithExitCode_WhenScriptExitsNonZero()
        {
            var runner = new FakeRunner(exitCode: 1);
            var probe = new FakeProbe(healthy: true);
            var launcher = new DevWebAppLauncher(probe, runner, readyTimeout: TimeSpan.FromSeconds(1), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.ScriptFailed, result.Status);
            Assert.Contains("exited with code 1", result.Message);
            Assert.Equal(0, probe.Calls);
        }

        [Fact]
        public async Task EnsureReady_WaitsForHealth_AfterScriptSucceeds()
        {
            var runner = new FakeRunner(exitCode: 0);
            var probe = new FakeProbe(healthyAfterCalls: 3);
            var launcher = new DevWebAppLauncher(probe, runner, readyTimeout: TimeSpan.FromSeconds(5), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.Ready, result.Status);
            Assert.Equal(3, probe.Calls);
        }

        [Fact]
        public async Task EnsureReady_ReportsNotReady_WhenHealthNeverPassesWithinTimeout()
        {
            var runner = new FakeRunner(exitCode: 0);
            var probe = new FakeProbe(healthy: false);
            var launcher = new DevWebAppLauncher(probe, runner, readyTimeout: TimeSpan.FromMilliseconds(50), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.NotReady, result.Status);
        }

        private sealed class FakeRunner : IHotswapScriptRunner
        {
            private readonly int? _exitCode;

            public FakeRunner(int? exitCode)
            {
                _exitCode = exitCode;
            }

            public int Calls { get; private set; }

            public string? LastScriptPath { get; private set; }

            public Task<int?> RunAsync(string scriptPath, CancellationToken ct)
            {
                Calls++;
                LastScriptPath = scriptPath;
                return Task.FromResult(_exitCode);
            }
        }

        private sealed class FakeProbe : IWebAppHealthProbe
        {
            private readonly bool _healthy;
            private readonly int _healthyAfterCalls;

            public FakeProbe(bool healthy)
            {
                _healthy = healthy;
                _healthyAfterCalls = int.MaxValue;
            }

            public FakeProbe(int healthyAfterCalls)
            {
                _healthy = false;
                _healthyAfterCalls = healthyAfterCalls;
            }

            public int Calls { get; private set; }

            public Task<bool> IsHealthyAsync(CancellationToken ct)
            {
                Calls++;
                return Task.FromResult(_healthy || Calls >= _healthyAfterCalls);
            }
        }
    }
}
