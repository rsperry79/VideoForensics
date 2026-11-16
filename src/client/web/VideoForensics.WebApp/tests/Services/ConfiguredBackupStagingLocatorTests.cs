using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Helpers.Platform;
using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services;

public class ConfiguredBackupStagingLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-backup-locator-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task GetExportDirectoryAsync_ReturnsConfiguredBackupRoot_AndCreatesIt()
    {
        var config = new Mock<IForensicsConfiguration>();
        _ = config.SetupGet(c => c.QueryExportLocation).Returns(_root);
        var configService = new Mock<IForensicsConfigurationService>();
        _ = configService.Setup(s => s.LoadConfigurationAsync(string.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(config.Object);
        var storage = new Mock<IStorageLocationProvider>();
        _ = storage.Setup(s => s.GetEffectiveRoot(StorageCategory.Backup, _root)).Returns(_root);

        var locator = new ConfiguredBackupStagingLocator(configService.Object, storage.Object);

        string dir = await locator.GetExportDirectoryAsync(CancellationToken.None);

        Assert.Equal(_root, dir);
        Assert.True(Directory.Exists(_root));
    }
}
