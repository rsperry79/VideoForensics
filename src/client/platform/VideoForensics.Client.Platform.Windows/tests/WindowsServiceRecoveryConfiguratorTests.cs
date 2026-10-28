using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class WindowsServiceRecoveryConfiguratorTests
    {
        [Fact]
        public void WindowsServiceRecoveryConfigurator_BuildFailureArguments_MatchesInstallerShape()
        {
            string args = WindowsServiceRecoveryConfigurator.BuildFailureArguments("VideoForensics");

            // Same actions the installer sets on a fresh install.
            Assert.Equal("failure VideoForensics reset= 86400 actions= restart/5000/restart/5000/restart/5000", args);
        }

        [Fact]
        public void WindowsServiceRecoveryConfigurator_BuildFailureFlagArguments_EnablesNonCrashFailures()
        {
            Assert.Equal("failureflag VideoForensics 1", WindowsServiceRecoveryConfigurator.BuildFailureFlagArguments("VideoForensics"));
        }

        [Fact]
        public void WindowsServiceRecoveryConfigurator_BuildQueryArguments_UseQueryVerbs()
        {
            Assert.Equal("qfailure VideoForensics", WindowsServiceRecoveryConfigurator.BuildQueryFailureArguments("VideoForensics"));
            Assert.Equal("qfailureflag VideoForensics", WindowsServiceRecoveryConfigurator.BuildQueryFailureFlagArguments("VideoForensics"));
        }

        [Theory]
        [InlineData("        FAILURE_ACTIONS             : RESTART -- Delay = 5000 milliseconds.", true)]
        [InlineData("        FAILURE_ACTIONS             : RUN PROCESS -- Delay = 5000 milliseconds.", false)]
        [InlineData("[SC] QueryServiceConfig2 SUCCESS\n\nSERVICE_NAME: VideoForensics\n        RESET_PERIOD (in seconds)    : 0", false)]
        public void WindowsServiceRecoveryConfigurator_HasRestartAction_ReadsScOutput(string output, bool expected)
        {
            Assert.Equal(expected, WindowsServiceRecoveryConfigurator.HasRestartAction(output));
        }

        [Theory]
        [InlineData("SERVICE_NAME: VideoForensics\n        FAILURE_ACTIONS_ON_NONCRASH_FAILURES: TRUE", true)]
        [InlineData("SERVICE_NAME: VideoForensics\n        FAILURE_ACTIONS_ON_NONCRASH_FAILURES: FALSE", false)]
        public void WindowsServiceRecoveryConfigurator_IsFailureFlagEnabled_ReadsScOutput(string output, bool expected)
        {
            Assert.Equal(expected, WindowsServiceRecoveryConfigurator.IsFailureFlagEnabled(output));
        }
    }
}
