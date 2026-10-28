using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Hosting.Updates;

namespace VideoForensics.Client.Hosting.Tests.Updates
{
    public class StartupUpdateApplierTests
    {
        private static StartupUpdateApplier NewApplier() => new(
            new UpdatePackageVerifier(),
            new UpdateApplier(NullLogger<UpdateApplier>.Instance),
            NullLogger<StartupUpdateApplier>.Instance);

        private static string Read(string dir, string relative) => File.ReadAllText(Path.Combine(dir, relative));

        [Fact]
        public async Task StartupUpdateApplier_NoUpdatesFolder_ReturnsNone()
        {
            using var temp = new UpdateTestFolder();

            StartupUpdateResult result = await NewApplier().TryApplyStagedAsync(
                Path.Combine(temp.Root, "missing"), temp.NewDir("install"), "1.0.0", true, CancellationToken.None);

            Assert.Equal(StartupUpdateOutcome.None, result.Outcome);
        }

        [Fact]
        public async Task StartupUpdateApplier_StagedPackage_AppliesAndClearsStagedFolder()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string install = temp.NewDir("install");
            UpdateTestFolder.WriteFile(install, "app.exe", "old-app");
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, "2.0.0"), "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new-app" });

            StartupUpdateResult result = await NewApplier().TryApplyStagedAsync(updates, install, "1.0.0", true, CancellationToken.None);

            Assert.Equal(StartupUpdateOutcome.Applied, result.Outcome);
            Assert.Equal("new-app", Read(install, "app.exe"));
            Assert.False(Directory.Exists(Path.Combine(updates, "2.0.0")));
            Assert.True(File.Exists(Path.Combine(updates, StartupUpdateApplier.StatusFileName)));
        }

        [Fact]
        public async Task StartupUpdateApplier_TamperedPackage_RejectsAndLeavesInstallUntouched()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string install = temp.NewDir("install");
            UpdateTestFolder.WriteFile(install, "app.exe", "old-app");
            string staged = Path.Combine(updates, "2.0.0");
            _ = UpdateTestFolder.BuildPackage(staged, "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new-app" });
            UpdateTestFolder.WriteFile(staged, "app.exe", "evil-app");

            StartupUpdateResult result = await NewApplier().TryApplyStagedAsync(updates, install, "1.0.0", true, CancellationToken.None);

            Assert.Equal(StartupUpdateOutcome.Rejected, result.Outcome);
            Assert.Equal("old-app", Read(install, "app.exe"));
            Assert.True(Directory.Exists(staged + StartupUpdateApplier.RejectedSuffix));
        }

        [Fact]
        public async Task StartupUpdateApplier_RejectedPackage_IsNotRetriedOnNextStart()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string install = temp.NewDir("install");
            string staged = Path.Combine(updates, "2.0.0");
            _ = UpdateTestFolder.BuildPackage(staged, "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new-app" });
            UpdateTestFolder.WriteFile(staged, "app.exe", "evil-app");
            StartupUpdateApplier applier = NewApplier();
            _ = await applier.TryApplyStagedAsync(updates, install, "1.0.0", true, CancellationToken.None);

            StartupUpdateResult second = await applier.TryApplyStagedAsync(updates, install, "1.0.0", true, CancellationToken.None);

            Assert.Equal(StartupUpdateOutcome.None, second.Outcome);
        }

        [Fact]
        public async Task StartupUpdateApplier_TwoStagedPackages_AppliesTheNewest()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string install = temp.NewDir("install");
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, "2.0.0"), "2.0.0", new Dictionary<string, string> { ["app.exe"] = "two" });
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, "2.10.0"), "2.10.0", new Dictionary<string, string> { ["app.exe"] = "two-ten" });

            StartupUpdateResult result = await NewApplier().TryApplyStagedAsync(updates, install, "1.0.0", true, CancellationToken.None);

            Assert.Equal(StartupUpdateOutcome.Applied, result.Outcome);
            Assert.Equal("two-ten", Read(install, "app.exe"));
        }

        [Fact]
        public async Task StartupUpdateApplier_StagingAndBackupFolders_AreNotTreatedAsPackages()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, UpdateStager.StagingFolderName, "abc"), "9.0.0", new Dictionary<string, string> { ["a.exe"] = "x" });
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, StartupUpdateApplier.BackupFolderName), "9.0.0", new Dictionary<string, string> { ["a.exe"] = "x" });

            StartupUpdateResult result = await NewApplier().TryApplyStagedAsync(updates, temp.NewDir("install"), "1.0.0", true, CancellationToken.None);

            Assert.Equal(StartupUpdateOutcome.None, result.Outcome);
        }

        [Fact]
        public async Task StartupUpdateApplier_KeepListEntries_SurviveTheUpdate()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string install = temp.NewDir("install");
            UpdateTestFolder.WriteFile(install, "ffmpeg.exe", "ffmpeg");
            UpdateTestFolder.WriteFile(install, "Desktop/client.exe", "client");
            _ = UpdateTestFolder.BuildPackage(Path.Combine(updates, "2.0.0"), "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new" });

            _ = await NewApplier().TryApplyStagedAsync(updates, install, "1.0.0", true, CancellationToken.None);

            Assert.Equal("ffmpeg", Read(install, "ffmpeg.exe"));
            Assert.Equal("client", Read(install, Path.Combine("Desktop", "client.exe")));
        }
    }
}
