using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class ServiceRestartSchedulerTests
    {
        [Fact]
        public void ServiceRestartScheduler_BuildArguments_WaitsThenStartsTheService()
        {
            string args = ServiceRestartScheduler.BuildArguments("VideoForensics", 5);

            Assert.Equal("/c timeout /t 5 /nobreak >nul & sc.exe start VideoForensics", args);
        }
    }
}
