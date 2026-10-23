namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>The export file must parse completely before an account is purged on its strength.</summary>
    public sealed class AccountExportVerifyTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "vf-export-verify-" + Guid.NewGuid().ToString("N"));

        public AccountExportVerifyTests()
        {
            _ = Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, recursive: true);
        }

        private string Write(string content)
        {
            string path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public async Task VerifyExportFileAsync_CompleteDocument_Passes()
        {
            string path = Write("{\"account\":{\"id\":\"1\"},\"items\":[1,2,3]}");

            await AccountExportService.VerifyExportFileAsync(path, CancellationToken.None);
        }

        [Fact]
        public async Task VerifyExportFileAsync_TruncatedDocument_Throws()
        {
            string path = Write("{\"account\":{\"id\":\"1\"},\"items\":[1,2,");

            _ = await Assert.ThrowsAnyAsync<Exception>(() => AccountExportService.VerifyExportFileAsync(path, CancellationToken.None));
        }

        [Fact]
        public async Task VerifyExportFileAsync_EmptyFile_Throws()
        {
            string path = Write(string.Empty);

            _ = await Assert.ThrowsAnyAsync<Exception>(() => AccountExportService.VerifyExportFileAsync(path, CancellationToken.None));
        }
    }
}
