using Xunit;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteAdminOperatorServiceTests
    {
        [Fact]
        public void UnlockAsync_MethodExists()
        {
            // Verify that RemoteAdminOperatorService has the UnlockAsync method
            var service = new RemoteAdminOperatorService(new HttpClient() { BaseAddress = new Uri("http://localhost") });

            // Verify the method exists and is accessible
            var method = typeof(RemoteAdminOperatorService).GetMethod("UnlockAsync");
            Assert.NotNull(method);
            Assert.True(method?.IsPublic);
        }

        [Fact]
        public void ImplementsIAdminOperatorService()
        {
            // Verify that RemoteAdminOperatorService implements IAdminOperatorService
            var service = new RemoteAdminOperatorService(new HttpClient() { BaseAddress = new Uri("http://localhost") });
            Assert.IsAssignableFrom<IAdminOperatorService>(service);
        }

        [Fact]
        public void HasCorrectHttpClientDependency()
        {
            // Verify that the service accepts an HttpClient in its constructor
            var httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteAdminOperatorService(httpClient);
            Assert.NotNull(service);
        }
    }
}
