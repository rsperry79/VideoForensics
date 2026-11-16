using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Core.Helpers.Platform;
using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api;

public class BackupExportRootTests
{
    [Fact]
    public void ResolveBackupExportRoot_UsesConfiguredQueryExportLocation()
    {
        var config = new Mock<IForensicsConfiguration>();
        _ = config.SetupGet(c => c.QueryExportLocation).Returns(@"D:\DbBackups");
        var storage = new Mock<IStorageLocationProvider>();
        _ = storage.Setup(s => s.GetEffectiveRoot(StorageCategory.Backup, @"D:\DbBackups")).Returns(@"D:\DbBackups");

        string root = BackupEndpoints.ResolveBackupExportRoot(storage.Object, config.Object);

        Assert.Equal(@"D:\DbBackups", root);
        storage.Verify(s => s.GetEffectiveRoot(StorageCategory.Backup, @"D:\DbBackups"), Times.Once);
    }
}
