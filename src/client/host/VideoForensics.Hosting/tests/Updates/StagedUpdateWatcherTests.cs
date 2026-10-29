using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Hosting.Updates;

namespace VideoForensics.Client.Hosting.Tests.Updates
{
    public class StagedUpdateWatcherTests
    {
        private static StartupUpdateApplier NewApplier(IUpdatePackageVerifier? verifier = null) => new(
            verifier ?? new UpdatePackageVerifier(),
            new UpdateApplier(NullLogger<UpdateApplier>.Instance),
            NullLogger<StartupUpdateApplier>.Instance);

        private static StagedUpdateWatcher NewWatcher(
            string updates, string install, Action<int> exit, IUpdateDrain? drain = null, IUpdatePackageVerifier? verifier = null) => new(
            updates,
            verifier ?? new UpdatePackageVerifier(),
            drain ?? new NoOpUpdateDrain(),
            NewApplier(verifier),
            install,
            allowDevPackages: true,
            () => "1.0.0",
            exit,
            TimeSpan.FromMilliseconds(10),
            NullLogger<StagedUpdateWatcher>.Instance);

        [Fact]
        public async Task StagedUpdateWatcher_NothingStaged_DoesNotExit()
        {
            using var temp = new UpdateTestFolder();
            var exits = new List<int>();

            bool found = await NewWatcher(temp.NewDir("updates"), temp.NewDir("install"), exits.Add).CheckOnceAsync(CancellationToken.None);

            Assert.False(found);
            Assert.Empty(exits);
        }

        [Fact]
        public async Task StagedUpdateWatcher_ValidPackage_DrainsAppliesThenExitsWithFailureCode()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string install = temp.NewDir("install");
            UpdateTestFolder.WriteFile(install, "app.exe", "old");
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, "2.0.0"), "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new" });
            var exits = new List<int>();
            var order = new List<string>();
            var drain = new Mock<IUpdateDrain>();
            _ = drain.Setup(d => d.DrainAsync(It.IsAny<CancellationToken>())).Callback(() => order.Add("drain")).Returns(Task.CompletedTask);

            bool found = await NewWatcher(updates, install, code =>
            {
                // By the time of the exit the new files must already be in place: the restart starts the new version.
                order.Add(File.ReadAllText(Path.Combine(install, "app.exe")) == "new" ? "exit-after-apply" : "exit-before-apply");
                exits.Add(code);
            }, drain.Object).CheckOnceAsync(CancellationToken.None);

            Assert.True(found);
            Assert.Equal([1], exits);
            Assert.Equal(["drain", "exit-after-apply"], order);
            Assert.False(Directory.Exists(Path.Combine(updates, "2.0.0")));
        }

        [Fact]
        public async Task StagedUpdateWatcher_TamperedPackage_DoesNotDrainApplyOrExit()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string install = temp.NewDir("install");
            UpdateTestFolder.WriteFile(install, "app.exe", "old");
            string staged = Path.Combine(updates, "2.0.0");
            _ = UpdateTestFolder.BuildPackage(staged, "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new" });
            UpdateTestFolder.WriteFile(staged, "app.exe", "evil");
            var exits = new List<int>();
            var drain = new Mock<IUpdateDrain>(MockBehavior.Strict);

            bool found = await NewWatcher(updates, install, exits.Add, drain.Object).CheckOnceAsync(CancellationToken.None);

            Assert.False(found);
            Assert.Empty(exits);
            Assert.Equal("old", File.ReadAllText(Path.Combine(install, "app.exe")));
        }

        [Fact]
        public async Task StagedUpdateWatcher_InvalidPackage_IsVerifiedOnlyOnce()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, "2.0.0"), "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new" });
            var verifier = new Mock<IUpdatePackageVerifier>();
            _ = verifier.Setup(v => v.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdatePackageVerification(UpdatePackageStatus.HashMismatch, "bad", null));
            StagedUpdateWatcher watcher = NewWatcher(updates, temp.NewDir("install"), _ => { }, verifier: verifier.Object);

            _ = await watcher.CheckOnceAsync(CancellationToken.None);
            _ = await watcher.CheckOnceAsync(CancellationToken.None);

            verifier.Verify(v => v.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StagedUpdateWatcher_StagingFolderOnly_DoesNotExit()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, UpdateStager.StagingFolderName, "abc"), "2.0.0", new Dictionary<string, string> { ["a"] = "x" });
            var exits = new List<int>();

            bool found = await NewWatcher(updates, temp.NewDir("install"), exits.Add).CheckOnceAsync(CancellationToken.None);

            Assert.False(found);
            Assert.Empty(exits);
        }

        [Fact]
        public async Task StagedUpdateWatcher_PackageAppearsLater_IsPickedUpByTheRunningLoop()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            StagedUpdateWatcher watcher = NewWatcher(updates, temp.NewDir("install"), exited.SetResult);

            await watcher.StartAsync(CancellationToken.None);
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, "2.0.0"), "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new" });
            Task finished = await Task.WhenAny(exited.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            await watcher.StopAsync(CancellationToken.None);

            Assert.Same(exited.Task, finished);
            Assert.Equal(1, await exited.Task);
        }
    }
}
