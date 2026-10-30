using VideoForensics.Client.Hosting.Updates;

namespace VideoForensics.Client.Hosting.Tests.Updates
{
    public class UpdateManifestTests
    {
        // Verbatim output of New-UpdateManifest in deploy/windows/stage-update-helpers.ps1. If the script's shape changes,
        // this test fails, because the service must still be able to read what the dev stage script writes.
        private const string StageScriptOutput = """
            {
              "version": "1.1.0-alpha.5+abc",
              "files": [
                {
                  "path": "app.exe",
                  "sha256": "2d711642b726b04401627ca9fbac32f5c8530fb1903cc4db02258717921a4881",
                  "size": 1
                },
                {
                  "path": "lib/core.dll",
                  "sha256": "ef90d9c1ec76b1edc9edfaf2c0c05359c10ccc49ae8ecf7b7fd25ce9c02e86a4",
                  "size": 2
                }
              ]
            }
            """;

        [Fact]
        public void UpdateManifest_TryParse_ReadsStageScriptOutput()
        {
            UpdateManifest? manifest = UpdateManifest.TryParse(StageScriptOutput);

            Assert.NotNull(manifest);
            Assert.Equal("1.1.0-alpha.5+abc", manifest.Version);
            Assert.Equal(2, manifest.Files.Count);
            Assert.Equal("lib/core.dll", manifest.Files[1].Path);
            Assert.Equal(2, manifest.Files[1].Size);
        }

        [Fact]
        public void UpdateManifest_ToJson_RoundTrips()
        {
            var original = new UpdateManifest("2.0.0", [new UpdateManifestFile("a.dll", new string('b', 64), 7)]);

            UpdateManifest? parsed = UpdateManifest.TryParse(original.ToJson());

            Assert.Equal(original.Version, parsed?.Version);
            Assert.Equal(original.Files[0], parsed?.Files[0]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("{}")]
        [InlineData("{ \"version\": \"\", \"files\": [] }")]
        [InlineData("not json")]
        public void UpdateManifest_TryParse_RejectsMalformedInput(string json)
        {
            Assert.Null(UpdateManifest.TryParse(json));
        }
    }
}
