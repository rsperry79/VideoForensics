using Microsoft.Extensions.Configuration;

using VideoForensics.Client.WebApp.Infrastructure;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Infrastructure
{
    public class InternalServerUrlTests
    {
        private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

        [Fact]
        public void ServerAddressResolver_ResolveDefaultInternalUrl_UsesHttpPortAndNeverLocalhost()
        {
            string url = ServerAddressResolver.ResolveDefaultInternalUrl(EmptyConfiguration());

            Assert.StartsWith("http://", url, StringComparison.Ordinal);
            Assert.EndsWith(":5162", url, StringComparison.Ordinal);
            Assert.DoesNotContain("//localhost:", url, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ServerAddressResolver_IsResolvableAsync_Localhost_ReturnsTrue()
        {
            bool resolvable = await ServerAddressResolver.IsResolvableAsync("localhost", CancellationToken.None);

            Assert.True(resolvable);
        }

        [Fact]
        public async Task ServerAddressResolver_IsResolvableAsync_InvalidTld_ReturnsFalse()
        {
            // The .invalid TLD is reserved and never resolves, so this check is deterministic.
            bool resolvable = await ServerAddressResolver.IsResolvableAsync("videoforensics-missing.invalid", CancellationToken.None);

            Assert.False(resolvable);
        }
    }
}
