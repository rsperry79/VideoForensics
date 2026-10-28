using Moq;

using VideoForensics.Client.Hosting.Updates;

namespace VideoForensics.Client.Hosting.Tests.Updates
{
    public class UpdatePackageVerifierTests
    {
        private static readonly Dictionary<string, string> TwoFiles = new()
        {
            ["app.exe"] = "binary-one",
            ["lib/core.dll"] = "binary-two",
        };

        private static IUpdatePackageVerifier DevVerifier() => new UpdatePackageVerifier();

        [Fact]
        public async Task UpdatePackageVerifier_ValidDevPackage_IsValid()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, "2.0.0", TwoFiles);

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, "1.0.0", allowDevPackages: true, CancellationToken.None);

            Assert.True(result.IsValid, result.Message);
            Assert.Equal("2.0.0", result.Manifest?.Version);
        }

        [Fact]
        public async Task UpdatePackageVerifier_NoManifest_ReportsManifestMissing()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            UpdateTestFolder.WriteFile(pkg, "app.exe", "x");

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, "1.0.0", true, CancellationToken.None);

            Assert.Equal(UpdatePackageStatus.ManifestMissing, result.Status);
        }

        [Fact]
        public async Task UpdatePackageVerifier_GarbageManifest_ReportsManifestInvalid()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            UpdateTestFolder.WriteFile(pkg, UpdateManifest.FileName, "{ not json");

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, "1.0.0", true, CancellationToken.None);

            Assert.Equal(UpdatePackageStatus.ManifestInvalid, result.Status);
        }

        [Fact]
        public async Task UpdatePackageVerifier_FileChangedAfterManifest_ReportsHashMismatch()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, "2.0.0", TwoFiles);
            UpdateTestFolder.WriteFile(pkg, "app.exe", "tampered!!"); // same length as "binary-one"

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, "1.0.0", true, CancellationToken.None);

            Assert.Equal(UpdatePackageStatus.HashMismatch, result.Status);
        }

        [Fact]
        public async Task UpdatePackageVerifier_FileSizeChanged_ReportsSizeMismatch()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, "2.0.0", TwoFiles);
            UpdateTestFolder.WriteFile(pkg, "app.exe", "longer than before");

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, "1.0.0", true, CancellationToken.None);

            Assert.Equal(UpdatePackageStatus.SizeMismatch, result.Status);
        }

        [Fact]
        public async Task UpdatePackageVerifier_FileMissing_ReportsFileMissing()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, "2.0.0", TwoFiles);
            File.Delete(Path.Combine(pkg, "app.exe"));

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, "1.0.0", true, CancellationToken.None);

            Assert.Equal(UpdatePackageStatus.FileMissing, result.Status);
        }

        [Theory]
        [InlineData("../evil.dll")]
        [InlineData("lib/../../evil.dll")]
        [InlineData("C:/Windows/evil.dll")]
        [InlineData("/etc/evil")]
        [InlineData("")]
        public async Task UpdatePackageVerifier_UnsafeManifestPath_ReportsUnsafePath(string path)
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            var manifest = new UpdateManifest("2.0.0", [new UpdateManifestFile(path, new string('a', 64), 1)]);
            UpdateTestFolder.WriteFile(pkg, UpdateManifest.FileName, manifest.ToJson());

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, "1.0.0", true, CancellationToken.None);

            Assert.Equal(UpdatePackageStatus.UnsafePath, result.Status);
        }

        [Theory]
        [InlineData("1.0.0", "1.0.0", false)]
        [InlineData("0.9.0", "1.0.0", false)]
        [InlineData("0.9.0", "1.0.0", true)]
        public async Task UpdatePackageVerifier_NotNewerVersion_IsRejected(string packageVersion, string current, bool allowDev)
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, packageVersion, TwoFiles);

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, current, allowDev, CancellationToken.None);

            Assert.Equal(UpdatePackageStatus.VersionNotNewer, result.Status);
        }

        [Fact]
        public async Task UpdatePackageVerifier_SameVersionDevPackage_IsAllowed()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, "1.0.0", TwoFiles);

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, "1.0.0", allowDevPackages: true, CancellationToken.None);

            Assert.True(result.IsValid, result.Message);
        }

        [Fact]
        public async Task UpdatePackageVerifier_PreReleaseSuffixOnPackage_ComparesNumericBase()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, "v2.0.0-testing.4", TwoFiles);

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, "1.9.0", true, CancellationToken.None);

            Assert.True(result.IsValid, result.Message);
        }

        [Fact]
        public async Task UpdatePackageVerifier_ReleaseModeWithoutSignatureVerifier_ReportsSignatureInvalid()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, "2.0.0", TwoFiles);

            UpdatePackageVerification result = await DevVerifier().VerifyAsync(pkg, "1.0.0", allowDevPackages: false, CancellationToken.None);

            Assert.Equal(UpdatePackageStatus.SignatureInvalid, result.Status);
        }

        [Fact]
        public async Task UpdatePackageVerifier_ReleaseModeSignatureRejected_ReportsSignatureInvalid()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, "2.0.0", TwoFiles);
            var signature = new Mock<IPackageSignatureVerifier>();
            _ = signature.Setup(s => s.VerifyAsync(pkg, It.IsAny<UpdateManifest>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            UpdatePackageVerification result = await new UpdatePackageVerifier(signature.Object)
                .VerifyAsync(pkg, "1.0.0", allowDevPackages: false, CancellationToken.None);

            Assert.Equal(UpdatePackageStatus.SignatureInvalid, result.Status);
        }

        [Fact]
        public async Task UpdatePackageVerifier_ReleaseModeSignatureAccepted_IsValid()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, "2.0.0", TwoFiles);
            var signature = new Mock<IPackageSignatureVerifier>();
            _ = signature.Setup(s => s.VerifyAsync(pkg, It.IsAny<UpdateManifest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            UpdatePackageVerification result = await new UpdatePackageVerifier(signature.Object)
                .VerifyAsync(pkg, "1.0.0", allowDevPackages: false, CancellationToken.None);

            Assert.True(result.IsValid, result.Message);
        }

        [Fact]
        public async Task UpdatePackageVerifier_DevMode_DoesNotCallSignatureVerifier()
        {
            using var temp = new UpdateTestFolder();
            string pkg = temp.NewDir("pkg");
            _ = UpdateTestFolder.BuildPackage(pkg, "2.0.0", TwoFiles);
            var signature = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);

            UpdatePackageVerification result = await new UpdatePackageVerifier(signature.Object)
                .VerifyAsync(pkg, "1.0.0", allowDevPackages: true, CancellationToken.None);

            Assert.True(result.IsValid, result.Message);
        }
    }
}
