using System.Diagnostics;

using VideoForensics.Client.Platform.Windows;

using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    /// <summary>
    /// The reaper puts child processes in a job object that kills them when its handle closes. Disposing it stands in for the
    /// service process exiting, which closes the same handle.
    /// </summary>
    public class WindowsJobChildProcessReaperTests
    {
        private static Process StartLongRunningChild()
        {
            return Process.Start(new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            })!;
        }

        [Fact]
        public void AdoptChildren_MatchingChild_IsKilledWhenTheReaperCloses()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var reaper = new WindowsJobChildProcessReaper();
            using Process child = StartLongRunningChild();
            try
            {
                int adopted = reaper.AdoptChildren("ping.exe");

                Assert.Equal(1, adopted);
                Assert.False(child.HasExited);

                reaper.Dispose();

                Assert.True(child.WaitForExit(5000), "the adopted child should have been killed when the job closed");
            }
            finally
            {
                if (!child.HasExited)
                {
                    child.Kill();
                }

                reaper.Dispose();
            }
        }

        [Fact]
        public void AdoptChildren_NoMatchingChild_ReturnsZero()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            using var reaper = new WindowsJobChildProcessReaper();

            Assert.Equal(0, reaper.AdoptChildren("no-such-program.exe"));
        }

        [Fact]
        public void AdoptChildren_CalledTwiceForTheSameChild_StillCountsIt()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            using var reaper = new WindowsJobChildProcessReaper();
            using Process child = StartLongRunningChild();
            try
            {
                Assert.Equal(1, reaper.AdoptChildren("ping.exe"));
                Assert.Equal(1, reaper.AdoptChildren("ping.exe"));
            }
            finally
            {
                child.Kill();
            }
        }

        [Fact]
        public void AdoptChildren_ProcessThatIsNotOurChild_IsLeftAlone()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            // This process is not its own child, so a name match alone must not adopt it.
            using var reaper = new WindowsJobChildProcessReaper();
            using Process self = Process.GetCurrentProcess();

            Assert.Equal(0, reaper.AdoptChildren(self.MainModule!.ModuleName));
        }
    }
}
