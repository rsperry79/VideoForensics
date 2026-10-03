using Microsoft.Extensions.Caching.Memory;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class TwoFactorPendingAuthCacheTests
    {
        [Fact]
        public void Store_ReturnsOpaqueToken()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new TwoFactorPendingAuthCache(memoryCache);
            var operatorId = Guid.NewGuid();

            // Act
            string token = cache.Store(operatorId);

            // Assert
            Assert.NotEmpty(token);
            Assert.Equal(32, token.Length); // GUIDs in N format are 32 chars

            memoryCache.Dispose();
        }

        [Fact]
        public void TryTake_WithValidToken_ReturnsOperatorId()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new TwoFactorPendingAuthCache(memoryCache);
            var operatorId = Guid.NewGuid();
            string token = cache.Store(operatorId);

            // Act
            Guid? result = cache.TryTake(token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(operatorId, result.Value);

            memoryCache.Dispose();
        }

        [Fact]
        public void TryTake_IsSingleUse_CannotTakeTwice()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new TwoFactorPendingAuthCache(memoryCache);
            var operatorId = Guid.NewGuid();
            string token = cache.Store(operatorId);

            // Act - first take succeeds
            Guid? first = cache.TryTake(token);
            // Second take should fail (entry was removed on first take)
            Guid? second = cache.TryTake(token);

            // Assert
            Assert.NotNull(first);
            Assert.Equal(operatorId, first.Value);
            Assert.Null(second);

            memoryCache.Dispose();
        }

        [Fact]
        public void TryTake_WithInvalidToken_ReturnsNull()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new TwoFactorPendingAuthCache(memoryCache);

            // Act
            Guid? result = cache.TryTake("invalid-token");

            // Assert
            Assert.Null(result);

            memoryCache.Dispose();
        }

        [Fact]
        public void StoreMultiple_EachHasUniqueToken()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new TwoFactorPendingAuthCache(memoryCache);
            var op1 = Guid.NewGuid();
            var op2 = Guid.NewGuid();

            // Act
            string token1 = cache.Store(op1);
            string token2 = cache.Store(op2);

            // Assert
            Assert.NotEqual(token1, token2);
            Assert.Equal(op1, cache.TryTake(token1));
            Assert.Equal(op2, cache.TryTake(token2));

            memoryCache.Dispose();
        }

        [Fact]
        public void Store_WithConcurrentAccess_EachTokenIsUnique()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new TwoFactorPendingAuthCache(memoryCache);
            var tokens = new System.Collections.Generic.HashSet<string>();
            var ids = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();

            // Act: Concurrent stores
            var tasks = ids.Select(id => Task.Run(() =>
            {
                string token = cache.Store(id);
                lock (tokens)
                {
                    tokens.Add(token);
                }
            })).ToArray();

            Task.WaitAll(tasks);

            // Assert: All tokens should be unique
            Assert.Equal(50, tokens.Count);

            memoryCache.Dispose();
        }
    }
}
