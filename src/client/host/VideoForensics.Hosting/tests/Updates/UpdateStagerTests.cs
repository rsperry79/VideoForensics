using System.IO.Compression;

using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Hosting.Updates;

namespace VideoForensics.Client.Hosting.Tests.Updates
{
    public class UpdateStagerTests
    {
        private static readonly Dictionary<string, string> Files = new()
        {
            ["app.exe"] = "binary-one",
            ["lib/core.dll"] = "binary-two",
        };

        private static UpdateStager NewStager(string updatesRoot, string currentVersion = "1.0.0") =>
            new(updatesRoot, new UpdatePackageVerifier(), allowDevPackages: true, () => currentVersion, NullLogger<UpdateStager>.Instance);

        private static string ZipPackage(UpdateTestFolder temp, string version, Action<string>? tamper = null)
        {
            string pkg = temp.NewDir("pkg-" + Guid.NewGuid().ToString("N"));
            _ = UpdateTestFolder.BuildPackage(pkg, version, Files);
            tamper?.Invoke(pkg);
            string zip = Path.Combine(temp.Root, Guid.NewGuid().ToString("N") + ".zip");
            ZipFile.CreateFromDirectory(pkg, zip);
            return zip;
        }

        [Fact]
        public async Task UpdateStager_ValidZip_StagesUnderVersionFolder()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");

            UpdateStageResult result = await NewStager(updates).StageZipAsync(ZipPackage(temp, "2.0.0"), CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.Equal(Path.Combine(updates, "2.0.0"), result.StagedDir);
            Assert.True(File.Exists(Path.Combine(updates, "2.0.0", UpdateManifest.FileName)));
            Assert.True(File.Exists(Path.Combine(updates, "2.0.0", "lib", "core.dll")));
        }

        [Fact]
        public async Task UpdateStager_ValidZip_LeavesNoStagingFolderBehind()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");

            _ = await NewStager(updates).StageZipAsync(ZipPackage(temp, "2.0.0"), CancellationToken.None);

            string staging = Path.Combine(updates, UpdateStager.StagingFolderName);
            Assert.False(Directory.Exists(staging) && Directory.EnumerateFileSystemEntries(staging).Any());
        }

        [Fact]
        public async Task UpdateStager_FileTamperedAfterManifest_RejectsAndStagesNothing()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string zip = ZipPackage(temp, "2.0.0", pkg => UpdateTestFolder.WriteFile(pkg, "app.exe", "tampered!!"));

            UpdateStageResult result = await NewStager(updates).StageZipAsync(zip, CancellationToken.None);

            Assert.False(result.Success);
            Assert.False(Directory.Exists(Path.Combine(updates, "2.0.0")));
        }

        [Fact]
        public async Task UpdateStager_NotNewerThanCurrent_Rejects()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");

            UpdateStageResult result = await NewStager(updates, currentVersion: "3.0.0")
                .StageZipAsync(ZipPackage(temp, "2.0.0"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.False(Directory.Exists(Path.Combine(updates, "2.0.0")));
        }

        [Fact]
        public async Task UpdateStager_ZipSlipEntry_RejectsAndWritesNothingOutside()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string zip = Path.Combine(temp.Root, "evil.zip");
            using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("../../escaped.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("pwned");
            }

            UpdateStageResult result = await NewStager(updates).StageZipAsync(zip, CancellationToken.None);

            Assert.False(result.Success);
            Assert.False(File.Exists(Path.Combine(temp.Root, "escaped.txt")));
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(temp.Root)!, "escaped.txt")));
        }

        [Fact]
        public async Task UpdateStager_CorruptZip_FailsWithoutThrowing()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            string zip = Path.Combine(temp.Root, "bad.zip");
            File.WriteAllText(zip, "not a zip");

            UpdateStageResult result = await NewStager(updates).StageZipAsync(zip, CancellationToken.None);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task UpdateStager_SameVersionStagedTwice_ReplacesPreviousFolder()
        {
            using var temp = new UpdateTestFolder();
            string updates = temp.NewDir("updates");
            UpdateStager stager = NewStager(updates);

            _ = await stager.StageZipAsync(ZipPackage(temp, "2.0.0"), CancellationToken.None);
            UpdateStageResult second = await stager.StageZipAsync(ZipPackage(temp, "2.0.0"), CancellationToken.None);

            Assert.True(second.Success, second.Message);
        }
    }
}
