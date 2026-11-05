using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    [Collection("StorageLocationStore")]
    public class StorageDefaultPath_Tests
    {
        [Fact]
        public void GetDefaultStorageRoot_SavedSettingsReadWithoutDefaultRow_UsesDefaultRootPointer()
        {
            // The database lives under the default storage root, so the pointer must still be read when the saved
            // DefaultStorageRoot row is absent. Ignoring it sends startup to the empty database on the system drive.
            string dataRoot = Path.Combine(Path.GetTempPath(), "vf-data-" + Guid.NewGuid().ToString("N"));
            string pointedRoot = Path.Combine(Path.GetTempPath(), "vf-pointed-" + Guid.NewGuid().ToString("N"));
            string? previousDefault = StorageLocationStore.DefaultRoot;
            bool previousSaved = StorageLocationStore.SavedSettingsRead;

            try
            {
                DefaultStorageRootPointer.Write(dataRoot, pointedRoot);
                StorageLocationStore.DefaultRoot = null;
                StorageLocationStore.SavedSettingsRead = true;

                var provider = new StorageLocationProvider(new NullStorageOverrideSource(), dataRoot);

                Assert.Equal(pointedRoot, provider.GetDefaultStorageRoot());
            }
            finally
            {
                StorageLocationStore.DefaultRoot = previousDefault;
                StorageLocationStore.SavedSettingsRead = previousSaved;
                if (Directory.Exists(dataRoot))
                {
                    Directory.Delete(dataRoot, recursive: true);
                }
            }
        }

        [Fact]
        public async Task GetStatusAsync_EveryDefaultCategory_CurrentPathMatchesPlatformDefault()
        {
            // A category reported as default must show the folder it actually uses, which is root\<Category> under
            // the default storage root. A stale saved Keys or Logs folder must not leak into a category shown as default.
            // Database was read and holds no Keys or Logs row (both reset to default). Legacy pointer files still point at
            // a stale folder, which the provider must ignore once saved settings were read.
            string dataRoot = Path.Combine(Path.GetTempPath(), "vf-data-" + Guid.NewGuid().ToString("N"));
            string defaultRoot = Path.Combine(Path.GetTempPath(), "vf-default-" + Guid.NewGuid().ToString("N"));
            string staleRoot = Path.Combine(Path.GetTempPath(), "vf-stale-" + Guid.NewGuid().ToString("N"));
            string? previousDefault = StorageLocationStore.DefaultRoot;
            string? previousKeys = StorageLocationStore.KeysRoot;
            string? previousLogs = StorageLocationStore.LogsRoot;
            bool previousSaved = StorageLocationStore.SavedSettingsRead;

            try
            {
                KeysLocationPointer.Write(dataRoot, staleRoot);
                LogsLocationPointer.Write(dataRoot, staleRoot);
                StorageLocationStore.DefaultRoot = defaultRoot;
                StorageLocationStore.KeysRoot = null;
                StorageLocationStore.LogsRoot = null;
                StorageLocationStore.SavedSettingsRead = true;

                var provider = new StorageLocationProvider(new NullStorageOverrideSource(), dataRoot);
                var configuration = new Mock<IForensicsConfigurationService>();
                _ = configuration
                    .Setup(x => x.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ForensicsConfiguration());

                var service = new StorageSettingsService(
                    provider,
                    configuration.Object,
                    new Mock<IDatabaseMaintenanceService>().Object,
                    new Mock<ILogger<StorageSettingsService>>().Object);

                StorageSettings settings = await service.GetStatusAsync(CancellationToken.None);

                var mismatches = new List<string>();
                foreach (StorageCategoryStatus status in settings.Categories.Where(s => s.IsDefault))
                {
                    string expected = provider.GetPlatformDefaultRoot(status.Category);
                    if (!string.Equals(status.CurrentPath, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        mismatches.Add($"{status.Category}: shows '{status.CurrentPath}' but default is '{expected}'");
                    }
                }

                Assert.Empty(mismatches);
            }
            finally
            {
                StorageLocationStore.DefaultRoot = previousDefault;
                StorageLocationStore.KeysRoot = previousKeys;
                StorageLocationStore.LogsRoot = previousLogs;
                StorageLocationStore.SavedSettingsRead = previousSaved;
                if (Directory.Exists(dataRoot))
                {
                    Directory.Delete(dataRoot, recursive: true);
                }
            }
        }
    }
}
