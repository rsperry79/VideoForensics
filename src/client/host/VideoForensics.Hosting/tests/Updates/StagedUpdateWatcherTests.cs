using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Hosting.Updates;

namespace VideoForensics.Client.Hosting.Tests.Updates
{
    public class StagedUpdateWatcherTests
    {
        private static StagedUpdateWatcher NewWatcher(string updates, List<int> exits, IUpdateDrain? drain = null, bool allowDev = true) => new(
            updates,
            new UpdatePackageVerifier(),
            drain ?? new NoOpUpdateDrain(),
            allowDev,
            () => "1.0.0",
            exits.Add,
            TimeSpan.FromMilliseconds(10),
            NullLogger<StagedUpdateWatcher>.Instance);

        [Fact]
        public async Task StagedUpdateWatcher_NothingStaged_DoesNotExit()
        {
            using var temp = new UpdateTestFolder();
            var exits = new List<int>();

            bool found = await NewWatcher(temp.NewDir("updates"), exits).CheckOnceAsync(CancellationToken.None);

            Assert.False(found);
            Assert.Empty(exits);
        }

        [Fact]
        public async Task StagedUpdateWatcher_ValidPackage_DrainsThenExitsWithFailureCode()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, "2.0.0"), "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new" });
            var exits = new List<int>();
            var order = new List<string>();
            var drain = new Mock<IUpdateDrain>();
            _ = drain.Setup(d => d.DrainAsync(It.IsAny<CancellationToken>())).Callback(() => order.Add("drain")).Returns(Task.CompletedTask);

            bool found = await new StagedUpdateWatcher(
                updates, new UpdatePackageVerifier(), drain.Object, allowDevPackages: true, () => "1.0.0",
                code => { order.Add("exit"); exits.Add(code); }, TimeSpan.FromMilliseconds(10), NullLogger<StagedUpdateWatcher>.Instance)
                .CheckOnceAsync(CancellationToken.None);

            Assert.True(found);
            Assert.Equal([1], exits);
            Assert.Equal(["drain", "exit"], order);
        }

        [Fact]
        public async Task StagedUpdateWatcher_TamperedPackage_DoesNotExitAndIsNotRetried()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string staged = Path.Combine(updates, "2.0.0");
            _ = UpdateTestFolder.BuildPackage(staged, "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new" });
            UpdateTestFolder.WriteFile(staged, "app.exe", "evil");
            var exits = new List<int>();
            var verifier = new Mock<IUpdatePackageVerifier>();
            _ = verifier.Setup(v => v.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdatePackageVerification(UpdatePackageStatus.HashMismatch, "bad", null));
            var watcher = new StagedUpdateWatcher(
                updates, verifier.Object, new NoOpUpdateDrain(), true, () => "1.0.0", exits.Add, TimeSpan.FromMilliseconds(10), NullLogger<StagedUpdateWatcher>.Instance);

            _ = await watcher.CheckOnceAsync(CancellationToken.None);
            _ = await watcher.CheckOnceAsync(CancellationToken.None);

            Assert.Empty(exits);
            verifier.Verify(v => v.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StagedUpdateWatcher_StagingFolderOnly_DoesNotExit()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, UpdateStager.StagingFolderName, "abc"), "2.0.0", new Dictionary<string, string> { ["a"] = "x" });
            var exits = new List<int>();

            bool found = await NewWatcher(updates, exits).CheckOnceAsync(CancellationToken.None);

            Assert.False(found);
            Assert.Empty(exits);
        }

        [Fact]
        public async Task StagedUpdateWatcher_PackageAppearsLater_IsPickedUpByTheRunningLoop()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var watcher = new StagedUpdateWatcher(
                updates, new UpdatePackageVerifier(), new NoOpUpdateDrain(), true, () => "1.0.0",
                exited.SetResult, TimeSpan.FromMilliseconds(10), NullLogger<StagedUpdateWatcher>.Instance);

            await watcher.StartAsync(CancellationToken.None);
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, "2.0.0"), "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new" });
            Task finished = await Task.WhenAny(exited.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            await watcher.StopAsync(CancellationToken.None);

            Assert.Same(exited.Task, finished);
            Assert.Equal(1, await exited.Task);
        }
    }
}
