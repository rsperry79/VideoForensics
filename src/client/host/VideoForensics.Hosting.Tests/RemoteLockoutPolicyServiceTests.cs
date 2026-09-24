using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteLockoutPolicyServiceTests
    {
        [Fact]
        public async Task GetAsync_ReturnsLockoutPolicySettings()
        {
            // This test verifies that RemoteLockoutPolicyService implements ILockoutPolicyService
            // and has the GetAsync method with correct signature
            var service = new RemoteLockoutPolicyService(new HttpClient() { BaseAddress = new Uri("http://localhost") });

            // Verify the method exists and is accessible
            var method = typeof(RemoteLockoutPolicyService).GetMethod("GetAsync");
            Assert.NotNull(method);
            Assert.True(method?.IsPublic);
            Assert.Equal(typeof(Task<LockoutPolicySettings>), method?.ReturnType);
        }

        [Fact]
        public async Task UpdateAsync_MethodExists()
        {
            // This test verifies that RemoteLockoutPolicyService has the UpdateAsync method
            var service = new RemoteLockoutPolicyService(new HttpClient() { BaseAddress = new Uri("http://localhost") });

            // Verify the method exists and is accessible
            var method = typeof(RemoteLockoutPolicyService).GetMethod("UpdateAsync");
            Assert.NotNull(method);
            Assert.True(method?.IsPublic);
        }

        [Fact]
        public void ImplementsILockoutPolicyService()
        {
            // Verify that RemoteLockoutPolicyService implements ILockoutPolicyService
            var service = new RemoteLockoutPolicyService(new HttpClient() { BaseAddress = new Uri("http://localhost") });
            Assert.IsAssignableFrom<Contracts.ILockoutPolicyService>(service);
        }
    }
}
