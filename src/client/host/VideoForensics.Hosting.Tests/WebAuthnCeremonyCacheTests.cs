using Microsoft.Extensions.Caching.Memory;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class WebAuthnCeremonyCacheTests
    {
        [Fact]
        public void Store_ReturnsOpaqueNonce()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new WebAuthnCeremonyCache(memoryCache);
            var optionsJson = """{"credentialCreateOptions":"test"}""";

            // Act
            string nonce = cache.Store(optionsJson);

            // Assert
            Assert.NotEmpty(nonce);
            Assert.Equal(32, nonce.Length); // GUIDs in N format are 32 chars

            memoryCache.Dispose();
        }

        [Fact]
        public void TryTake_WithValidNonce_ReturnsOptionsJson()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new WebAuthnCeremonyCache(memoryCache);
            var optionsJson = """{"credentialCreateOptions":"test"}""";
            string nonce = cache.Store(optionsJson);

            // Act
            string? result = cache.TryTake(nonce);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(optionsJson, result);

            memoryCache.Dispose();
        }

        [Fact]
        public void TryTake_IsSingleUse_CannotTakeTwice()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new WebAuthnCeremonyCache(memoryCache);
            var optionsJson = """{"credentialCreateOptions":"test"}""";
            string nonce = cache.Store(optionsJson);

            // Act - first take succeeds
            string? first = cache.TryTake(nonce);
            // Second take should fail (entry was removed on first take)
            string? second = cache.TryTake(nonce);

            // Assert
            Assert.NotNull(first);
            Assert.Equal(optionsJson, first);
            Assert.Null(second);

            memoryCache.Dispose();
        }

        [Fact]
        public void TryTake_WithInvalidNonce_ReturnsNull()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new WebAuthnCeremonyCache(memoryCache);

            // Act
            string? result = cache.TryTake("invalid-nonce");

            // Assert
            Assert.Null(result);

            memoryCache.Dispose();
        }

        [Fact]
        public void StoreMultiple_EachHasUniqueNonce()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new WebAuthnCeremonyCache(memoryCache);
            var json1 = """{"credentialCreateOptions":"test1"}""";
            var json2 = """{"credentialCreateOptions":"test2"}""";

            // Act
            string nonce1 = cache.Store(json1);
            string nonce2 = cache.Store(json2);

            // Assert
            Assert.NotEqual(nonce1, nonce2);
            Assert.Equal(json1, cache.TryTake(nonce1));
            Assert.Equal(json2, cache.TryTake(nonce2));

            memoryCache.Dispose();
        }

        [Fact]
        public async Task Store_WithConcurrentAccess_EachNonceIsUnique()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new WebAuthnCeremonyCache(memoryCache);
            var nonces = new System.Collections.Generic.HashSet<string>();
            var jsonOptions = Enumerable.Range(0, 50)
                .Select(i => $$$"""{"credentialCreateOptions":"test{{{i}}}"}""")
                .ToList();

            // Act: Concurrent stores
            var tasks = jsonOptions.Select(json => Task.Run(() =>
            {
                string nonce = cache.Store(json);
                lock (nonces)
                {
                    nonces.Add(nonce);
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            // Assert: All nonces should be unique
            Assert.Equal(50, nonces.Count);

            memoryCache.Dispose();
        }
    }
}
