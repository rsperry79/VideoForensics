using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Hosting.Updates;

namespace VideoForensics.Client.Hosting.Tests.Updates
{
    public class UpdateApplierTests
    {
        private static readonly string[] KeepList = ["Desktop", "ffmpeg.exe"];

        private static UpdateApplier NewApplier() => new(NullLogger<UpdateApplier>.Instance);

        private static string Read(string dir, string relative) =>
            File.ReadAllText(Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar)));

        private static bool Exists(string dir, string relative) =>
            File.Exists(Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar)));

        [Fact]
        public async Task UpdateApplier_Apply_NewPackage_ReplacesChangedAddsNewAndRemovesDropped()
        {
            using var temp = new UpdateTestFolder();
            string install = temp.NewDir("install");
            string pkg = temp.NewDir("pkg");
            string backup = Path.Combine(temp.Root, "backup");
            UpdateTestFolder.WriteFile(install, "app.exe", "old-app");
            UpdateTestFolder.WriteFile(install, "old.dll", "dropped");
            UpdateManifest manifest = UpdateTestFolder.BuildPackage(pkg, "2.0.0", new Dictionary<string, string>
            {
                ["app.exe"] = "new-app",
                ["lib/new.dll"] = "added",
            });

            UpdateApplyResult result = await NewApplier().ApplyAsync(pkg, manifest, install, backup, KeepList, CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Equal("new-app", Read(install, "app.exe"));
            Assert.Equal("added", Read(install, "lib/new.dll"));
            Assert.False(Exists(install, "old.dll"));
        }

        [Fact]
        public async Task UpdateApplier_Apply_KeepListEntries_AreNeverRemoved()
        {
            using var temp = new UpdateTestFolder();
            string install = temp.NewDir("install");
            string pkg = temp.NewDir("pkg");
            UpdateTestFolder.WriteFile(install, "Desktop/client.exe", "desktop");
            UpdateTestFolder.WriteFile(install, "ffmpeg.exe", "ffmpeg");
            UpdateTestFolder.WriteFile(install, "DesktopTools/x.dll", "not-kept");
            UpdateManifest manifest = UpdateTestFolder.BuildPackage(pkg, "2.0.0", new Dictionary<string, string> { ["app.exe"] = "a" });

            UpdateApplyResult result = await NewApplier().ApplyAsync(pkg, manifest, install, Path.Combine(temp.Root, "backup"), KeepList, CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Equal("desktop", Read(install, "Desktop/client.exe"));
            Assert.Equal("ffmpeg", Read(install, "ffmpeg.exe"));
            Assert.False(Exists(install, "DesktopTools/x.dll"));
        }

        [Fact]
        public async Task UpdateApplier_Apply_EmptyFoldersLeftBehind_AreRemoved()
        {
            using var temp = new UpdateTestFolder();
            string install = temp.NewDir("install");
            string pkg = temp.NewDir("pkg");
            UpdateTestFolder.WriteFile(install, "gone/only.dll", "x");
            UpdateManifest manifest = UpdateTestFolder.BuildPackage(pkg, "2.0.0", new Dictionary<string, string> { ["app.exe"] = "a" });

            _ = await NewApplier().ApplyAsync(pkg, manifest, install, Path.Combine(temp.Root, "backup"), KeepList, CancellationToken.None);

            Assert.False(Directory.Exists(Path.Combine(install, "gone")));
        }

        [Fact]
        public async Task UpdateApplier_Apply_UnsafeManifestPath_ChangesNothing()
        {
            using var temp = new UpdateTestFolder();
            string install = temp.NewDir("install");
            string pkg = temp.NewDir("pkg");
            UpdateTestFolder.WriteFile(install, "app.exe", "old-app");
            var manifest = new UpdateManifest("2.0.0", [new UpdateManifestFile("../escape.dll", new string('a', 64), 1)]);

            UpdateApplyResult result = await NewApplier().ApplyAsync(pkg, manifest, install, Path.Combine(temp.Root, "backup"), KeepList, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("old-app", Read(install, "app.exe"));
            Assert.False(File.Exists(Path.Combine(temp.Root, "escape.dll")));
        }

        [Fact]
        public async Task UpdateApplier_Apply_FailsMidway_RestoresPreviousVersion()
        {
            using var temp = new UpdateTestFolder();
            string install = temp.NewDir("install");
            string pkg = temp.NewDir("pkg");
            UpdateTestFolder.WriteFile(install, "app.exe", "old-app");
            UpdateTestFolder.WriteFile(install, "old.dll", "dropped");
            UpdateManifest manifest = UpdateTestFolder.BuildPackage(pkg, "2.0.0", new Dictionary<string, string>
            {
                ["app.exe"] = "new-app",
                ["z-last.dll"] = "last",
            });

            // Deleting a package source after verification makes the copy of the last file throw mid-apply.
            File.Delete(Path.Combine(pkg, "z-last.dll"));

            UpdateApplyResult result = await NewApplier().ApplyAsync(pkg, manifest, install, Path.Combine(temp.Root, "backup"), KeepList, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("old-app", Read(install, "app.exe"));
            Assert.Equal("dropped", Read(install, "old.dll"));
            Assert.False(Exists(install, "z-last.dll"));
        }

        [Fact]
        public async Task UpdateApplier_Rollback_AfterSuccessfulApply_RestoresPreviousVersion()
        {
            using var temp = new UpdateTestFolder();
            string install = temp.NewDir("install");
            string pkg = temp.NewDir("pkg");
            string backup = Path.Combine(temp.Root, "backup");
            UpdateTestFolder.WriteFile(install, "app.exe", "old-app");
            UpdateTestFolder.WriteFile(install, "old.dll", "dropped");
            UpdateManifest manifest = UpdateTestFolder.BuildPackage(pkg, "2.0.0", new Dictionary<string, string>
            {
                ["app.exe"] = "new-app",
                ["lib/new.dll"] = "added",
            });
            UpdateApplier applier = NewApplier();
            _ = await applier.ApplyAsync(pkg, manifest, install, backup, KeepList, CancellationToken.None);

            UpdateApplyResult result = await applier.RollbackAsync(install, backup, CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Equal("old-app", Read(install, "app.exe"));
            Assert.Equal("dropped", Read(install, "old.dll"));
            Assert.False(Exists(install, "lib/new.dll"));
        }

        [Fact]
        public async Task UpdateApplier_Apply_LockedFile_IsRenamedAsideAndReplaced()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // The rename-while-open behavior this covers is Windows-specific.
            }

            using var temp = new UpdateTestFolder();
            string install = temp.NewDir("install");
            string pkg = temp.NewDir("pkg");
            UpdateTestFolder.WriteFile(install, "app.exe", "old-app");
            UpdateTestFolder.WriteFile(install, "old.dll", "dropped");
            UpdateManifest manifest = UpdateTestFolder.BuildPackage(pkg, "2.0.0", new Dictionary<string, string> { ["app.exe"] = "new-app" });

            // FileShare.Delete mimics a loaded image: it blocks overwrite and delete but still allows a rename.
            using var lockApp = new FileStream(Path.Combine(install, "app.exe"), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var lockOld = new FileStream(Path.Combine(install, "old.dll"), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

            UpdateApplyResult result = await NewApplier().ApplyAsync(pkg, manifest, install, Path.Combine(temp.Root, "backup"), KeepList, CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Equal("new-app", Read(install, "app.exe"));
            Assert.False(Exists(install, "old.dll"));

            // The overwritten file cannot be replaced in place, so it is renamed aside. The dropped file is deletable
            // while open with FileShare.Delete (a mapped image would not be; SetAside falls back to the same rename).
            Assert.Single(Directory.GetFiles(install, "app.exe.old-*"));
        }

        [Fact]
        public async Task UpdateApplier_DeleteLeftoverFiles_RemovesUnlockedOldFilesOnly()
        {
            using var temp = new UpdateTestFolder();
            string install = temp.NewDir("install");
            UpdateTestFolder.WriteFile(install, "app.exe", "current");
            UpdateTestFolder.WriteFile(install, "app.exe.old-abc", "stale");
            UpdateTestFolder.WriteFile(install, "lib/core.dll.old-def", "stale");

            UpdateApplier.DeleteLeftoverFiles(install);

            Assert.True(Exists(install, "app.exe"));
            Assert.Empty(Directory.GetFiles(install, "*.old-*", SearchOption.AllDirectories));
            await Task.CompletedTask;
        }

        [Fact]
        public async Task UpdateApplier_Rollback_NoBackup_ReportsFailure()
        {
            using var temp = new UpdateTestFolder();

            UpdateApplyResult result = await NewApplier().RollbackAsync(temp.NewDir("install"), Path.Combine(temp.Root, "none"), CancellationToken.None);

            Assert.False(result.Success);
        }
    }
}
