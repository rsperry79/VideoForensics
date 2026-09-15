using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteForensicsConfigurationServiceTests
    {
        [Fact]
        public async Task LoadConfigurationAsync_Always_ThrowsNotSupportedException()
        {
            var httpClient = new HttpClient() { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteForensicsConfigurationService(httpClient);

            NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                _ = await service.LoadConfigurationAsync("/path/to/config.json", CancellationToken.None);
            });

            Assert.NotNull(exception);
            Assert.Contains("Not supported on a remote", exception.Message);
        }

        [Fact]
        public async Task LoadConfigurationAsync_WithCancellationToken_StillThrowsNotSupportedException()
        {
            var httpClient = new HttpClient() { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteForensicsConfigurationService(httpClient);

            _ = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                _ = await service.LoadConfigurationAsync("/path/to/config.json", new CancellationTokenSource().Token);
            });
        }

        [Fact]
        public async Task LoadConfigurationAsync_WithDifferentPaths_AlwaysThrows()
        {
            var httpClient = new HttpClient() { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteForensicsConfigurationService(httpClient);

            _ = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                _ = await service.LoadConfigurationAsync("", CancellationToken.None);
            });

            _ = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                _ = await service.LoadConfigurationAsync("C:\\config.json", CancellationToken.None);
            });
        }

        [Fact]
        public async Task SaveConfigurationAsync_Always_ThrowsNotSupportedException()
        {
            var httpClient = new HttpClient() { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteForensicsConfigurationService(httpClient);
            var config = new ForensicsConfiguration();

            NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await service.SaveConfigurationAsync(config, CancellationToken.None);
            });

            Assert.NotNull(exception);
            Assert.Contains("Not supported on a remote", exception.Message);
        }

        [Fact]
        public async Task SaveConfigurationAsync_WithCancellationToken_StillThrowsNotSupportedException()
        {
            var httpClient = new HttpClient() { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteForensicsConfigurationService(httpClient);
            var config = new ForensicsConfiguration();

            _ = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await service.SaveConfigurationAsync(config, new CancellationTokenSource().Token);
            });
        }
    }
}
