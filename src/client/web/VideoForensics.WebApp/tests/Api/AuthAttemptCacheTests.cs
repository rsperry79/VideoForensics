using Microsoft.Extensions.Caching.Memory;

using VideoForensics.Client.WebApp.Api;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    public class AuthAttemptCacheTests
    {
        [Fact]
        public void StoreAttempt_ReturnsGuid()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new AuthAttemptCache(memoryCache);

            // Act
            Guid attemptId = cache.StoreAttempt("providerName", "username", "password");

            // Assert
            Assert.NotEqual(Guid.Empty, attemptId);

            memoryCache.Dispose();
        }

        [Fact]
        public void GetAndRemoveAttempt_WithValidAttemptId_ReturnsStoredCredentials()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new AuthAttemptCache(memoryCache);
            string provider = "ringProvider";
            string username = "test@example.com";
            string password = "SecurePassword123!";

            Guid attemptId = cache.StoreAttempt(provider, username, password);

            // Act
            (string? ProviderName, string? Username, string? Password)? result = cache.GetAndRemoveAttempt(attemptId);

            // Assert
            _ = Assert.NotNull(result);
            Assert.Equal(provider, result.Value.ProviderName);
            Assert.Equal(username, result.Value.Username);
            Assert.Equal(password, result.Value.Password);

            memoryCache.Dispose();
        }

        [Fact]
        public void GetAndRemoveAttempt_WithInvalidAttemptId_ReturnsNull()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new AuthAttemptCache(memoryCache);

            // Act
            (string? ProviderName, string? Username, string? Password)? result = cache.GetAndRemoveAttempt(Guid.NewGuid());

            // Assert
            Assert.Null(result);

            memoryCache.Dispose();
        }

        [Fact]
        public void GetAndRemoveAttempt_IsSingleUse_CannotRemoveTwice()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new AuthAttemptCache(memoryCache);
            Guid attemptId = cache.StoreAttempt("provider", "username", "password");

            // Act - first retrieval succeeds
            (string? ProviderName, string? Username, string? Password)? first = cache.GetAndRemoveAttempt(attemptId);
            // Second retrieval should fail (entry was removed on first call)
            (string? ProviderName, string? Username, string? Password)? second = cache.GetAndRemoveAttempt(attemptId);

            // Assert
            _ = Assert.NotNull(first);
            Assert.Null(second);

            memoryCache.Dispose();
        }

        [Fact]
        public void StoreAttempt_WithNullProvider_Succeeds()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new AuthAttemptCache(memoryCache);

            // Act
            Guid attemptId = cache.StoreAttempt(null, "username", "password");
            (string? ProviderName, string? Username, string? Password)? result = cache.GetAndRemoveAttempt(attemptId);

            // Assert
            _ = Assert.NotNull(result);
            Assert.Null(result.Value.ProviderName);
            Assert.Equal("username", result.Value.Username);
            Assert.Equal("password", result.Value.Password);

            memoryCache.Dispose();
        }

        [Fact]
        public void StoreMultiple_EachHasUniqueAttemptId()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new AuthAttemptCache(memoryCache);

            // Act
            Guid id1 = cache.StoreAttempt("provider1", "user1", "pass1");
            Guid id2 = cache.StoreAttempt("provider2", "user2", "pass2");

            // Assert
            Assert.NotEqual(id1, id2);

            (string? ProviderName, string? Username, string? Password)? result1 = cache.GetAndRemoveAttempt(id1);
            (string? ProviderName, string? Username, string? Password)? result2 = cache.GetAndRemoveAttempt(id2);

            _ = Assert.NotNull(result1);
            _ = Assert.NotNull(result2);
            Assert.Equal("user1", result1.Value.Username);
            Assert.Equal("user2", result2.Value.Username);

            memoryCache.Dispose();
        }

        [Fact]
        public async Task StoreAttempt_WithConcurrentAccess_EachAttemptIdIsUnique()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new AuthAttemptCache(memoryCache);
            var attemptIds = new System.Collections.Generic.HashSet<Guid>();

            // Act: Concurrent stores
            Task[] tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
            {
                Guid id = cache.StoreAttempt($"provider{i}", $"user{i}", "password");
                lock (attemptIds)
                {
                    _ = attemptIds.Add(id);
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            // Assert: All attempt IDs should be unique
            Assert.Equal(50, attemptIds.Count);

            memoryCache.Dispose();
        }

        [Fact]
        public async Task GetAndRemoveAttempt_ConcurrentRetrievals_EachConsumesOnce()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new AuthAttemptCache(memoryCache);
            var attemptIds = Enumerable.Range(0, 10)
                .Select(i => cache.StoreAttempt($"provider{i}", $"user{i}", "password"))
                .ToList();

            int successCount = 0;
            object lockObj = new();

            // Act: Concurrent retrievals on different attempt IDs
            Task[] tasks = attemptIds.Select(id => Task.Run(() =>
            {
                (string? ProviderName, string? Username, string? Password)? result = cache.GetAndRemoveAttempt(id);
                if (result != null)
                {
                    lock (lockObj)
                    {
                        successCount++;
                    }
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            // Assert: All attempts should be successfully retrieved once
            Assert.Equal(10, successCount);

            memoryCache.Dispose();
        }
    }
}
