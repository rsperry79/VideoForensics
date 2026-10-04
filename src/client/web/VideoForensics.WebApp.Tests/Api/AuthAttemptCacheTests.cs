using Microsoft.Extensions.Caching.Memory;

using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api
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
            var result = cache.GetAndRemoveAttempt(attemptId);

            // Assert
            Assert.NotNull(result);
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
            var result = cache.GetAndRemoveAttempt(Guid.NewGuid());

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
            var first = cache.GetAndRemoveAttempt(attemptId);
            // Second retrieval should fail (entry was removed on first call)
            var second = cache.GetAndRemoveAttempt(attemptId);

            // Assert
            Assert.NotNull(first);
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
            var result = cache.GetAndRemoveAttempt(attemptId);

            // Assert
            Assert.NotNull(result);
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

            var result1 = cache.GetAndRemoveAttempt(id1);
            var result2 = cache.GetAndRemoveAttempt(id2);

            Assert.NotNull(result1);
            Assert.NotNull(result2);
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
            var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
            {
                Guid id = cache.StoreAttempt($"provider{i}", $"user{i}", "password");
                lock (attemptIds)
                {
                    attemptIds.Add(id);
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

            var successCount = 0;
            var lockObj = new object();

            // Act: Concurrent retrievals on different attempt IDs
            var tasks = attemptIds.Select(id => Task.Run(() =>
            {
                var result = cache.GetAndRemoveAttempt(id);
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
