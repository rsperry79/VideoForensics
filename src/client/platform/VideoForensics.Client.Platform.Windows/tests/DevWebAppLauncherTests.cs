using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class DevWebAppLauncherTests
    {
        private const string ScriptPath = @"C:\repo\deploy\windows\stage-update.ps1";

        [Fact]
        public void BuildArguments_PublishesBeforeStaging_AndQuotesPath()
        {
            // Without -Build the script looks for an existing publish folder and fails when none exists.
            string arguments = StageScriptRunner.BuildArguments(@"C:\My Repo\deploy\windows\stage-update.ps1");

            Assert.Contains("-Build", arguments);
            Assert.Contains("-File \"C:\\My Repo\\deploy\\windows\\stage-update.ps1\"", arguments);
        }

        [Fact]
        public async Task EnsureReady_RunsScriptEvenWhenServerAlreadyHealthy()
        {
            // Staging must run on every launch so new binaries reach the service; a healthy probe must not skip it.
            var runner = new FakeRunner(exitCode: 0);
            var probe = new FakeProbe(healthy: true);
            var launcher = new DevWebAppLauncher(probe, runner, () => false, readyTimeout: TimeSpan.FromSeconds(1), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.Ready, result.Status);
            Assert.Equal(1, runner.Calls);
            Assert.Equal(ScriptPath, runner.LastScriptPath);
        }

        [Fact]
        public async Task EnsureReady_ReportsNotRun_WhenScriptCannotStart()
        {
            var runner = new FakeRunner(exitCode: null);
            var probe = new FakeProbe(healthy: true);
            var launcher = new DevWebAppLauncher(probe, runner, () => false, readyTimeout: TimeSpan.FromSeconds(1), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.ScriptNotRun, result.Status);
            Assert.Equal(0, probe.Calls);
        }

        [Fact]
        public async Task EnsureReady_ReportsScriptFailed_WithExitCode_WhenScriptExitsNonZero()
        {
            var runner = new FakeRunner(exitCode: 1);
            var probe = new FakeProbe(healthy: true);
            var launcher = new DevWebAppLauncher(probe, runner, () => false, readyTimeout: TimeSpan.FromSeconds(1), pollInterval: TimeSpan.FromMilliseconds(5));

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
            var launcher = new DevWebAppLauncher(probe, runner, () => false, readyTimeout: TimeSpan.FromSeconds(5), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.Ready, result.Status);
            Assert.Equal(3, probe.Calls);
        }

        [Fact]
        public async Task EnsureReady_ReportsNotReady_WhenHealthNeverPassesWithinTimeout()
        {
            var runner = new FakeRunner(exitCode: 0);
            var probe = new FakeProbe(healthy: false);
            var launcher = new DevWebAppLauncher(probe, runner, () => false, readyTimeout: TimeSpan.FromMilliseconds(50), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.NotReady, result.Status);
        }

        [Fact]
        public async Task EnsureReady_WaitsForStagedUpdateToBeConsumed_BeforeAcceptingHealth()
        {
            // The old instance keeps answering /healthz until it restarts; health alone must not count as ready.
            var runner = new FakeRunner(exitCode: 0);
            var probe = new FakeProbe(healthy: true);
            int checks = 0;
            var launcher = new DevWebAppLauncher(probe, runner, () => ++checks < 4, readyTimeout: TimeSpan.FromSeconds(5), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.Ready, result.Status);
            Assert.Equal(4, checks);
            Assert.Equal(1, probe.Calls);
        }

        [Fact]
        public async Task EnsureReady_ReportsNotReady_WhenStagedUpdateIsNeverApplied()
        {
            var runner = new FakeRunner(exitCode: 0);
            var probe = new FakeProbe(healthy: true);
            var launcher = new DevWebAppLauncher(probe, runner, () => true, readyTimeout: TimeSpan.FromMilliseconds(50), pollInterval: TimeSpan.FromMilliseconds(5));

            DevWebAppLaunchResult result = await launcher.EnsureReadyAsync(ScriptPath, CancellationToken.None);

            Assert.Equal(DevWebAppLaunchStatus.NotReady, result.Status);
            Assert.Contains("did not apply the staged update", result.Message);
            Assert.Equal(0, probe.Calls);
        }

        private sealed class FakeRunner : IStageScriptRunner
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
