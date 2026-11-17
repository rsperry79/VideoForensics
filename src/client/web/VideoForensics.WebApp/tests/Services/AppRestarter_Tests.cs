using Microsoft.Extensions.Hosting;

using Moq;

using VideoForensics.Client.WebApp.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class AppRestarter_Tests
    {
        private readonly Mock<IHostApplicationLifetime> _lifetime = new();

        [Fact]
        public void Restart_OutsideAService_StartsAFreshCopyAfterThisProcessExits()
        {
            var launched = new List<string>();
            var waitedFor = new List<int>();
            var scheduled = new List<string>();
            var restarter = new AppRestarter(_lifetime.Object, (pid, command) => { waitedFor.Add(pid); launched.Add(command); }, () => false, name => { scheduled.Add(name); return true; }, @"C:\VideoForensics\WebApp.exe", ["--urls", "http://localhost:5162"]);

            restarter.Restart();

            _ = Assert.Single(launched);
            Assert.Equal(new[] { Environment.ProcessId }, waitedFor);
            Assert.Contains(@"C:\VideoForensics\WebApp.exe", launched[0]);
            Assert.Contains("--urls", launched[0]);
            Assert.Empty(scheduled);
            _lifetime.Verify(l => l.StopApplication(), Times.Once);
        }

        [Fact]
        public void Restart_ArgumentWithQuotes_IsEscapedSoTheRelaunchKeepsItsValue()
        {
            // A quote inside an argument must not end the quoted token early.
            var launched = new List<string>();
            var restarter = new AppRestarter(_lifetime.Object, (pid, command) => launched.Add(command), () => false, name => true, @"C:\VideoForensics\WebApp.exe", ["--name", @"say ""hi"""]);

            restarter.Restart();

            _ = Assert.Single(launched);
            Assert.Contains(@"""say \""hi\""""", launched[0]);
        }

        [Fact]
        public void Restart_ArgumentEndingInBackslash_KeepsTheClosingQuote()
        {
            // An odd run of trailing backslashes would otherwise escape the closing quote.
            var launched = new List<string>();
            var restarter = new AppRestarter(_lifetime.Object, (pid, command) => launched.Add(command), () => false, name => true, @"C:\VideoForensics\WebApp.exe", [@"C:\data\"]);

            restarter.Restart();

            _ = Assert.Single(launched);
            Assert.Contains(@"""C:\data\\""", launched[0]);
        }

        [Fact]
        public void Restart_AsAWindowsService_SchedulesTheServiceStartAndDoesNotLaunchACopy()
        {
            var launched = new List<string>();
            var scheduled = new List<string>();
            var restarter = new AppRestarter(_lifetime.Object, (pid, command) => launched.Add(command), () => true, name => { scheduled.Add(name); return true; }, @"C:\VideoForensics\WebApp.exe", []);

            restarter.Restart();

            Assert.Empty(launched);
            Assert.Equal(new[] { "VideoForensics" }, scheduled);
            _lifetime.Verify(l => l.StopApplication(), Times.Once);
        }
    }
}
