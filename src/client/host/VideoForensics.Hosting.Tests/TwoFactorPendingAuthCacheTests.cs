using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class TwoFactorPendingAuthCacheTests
    {
        [Fact]
        public void Store_ReturnsOpaqueToken()
        {
            // Arrange
            var cache = new TwoFactorPendingAuthCache();
            var operatorId = Guid.NewGuid();

            // Act
            string token = cache.Store(operatorId);

            // Assert
            Assert.NotEmpty(token);
            Assert.Equal(32, token.Length); // GUIDs in N format are 32 chars
        }

        [Fact]
        public void TryTake_WithValidToken_ReturnsOperatorId()
        {
            // Arrange
            var cache = new TwoFactorPendingAuthCache();
            var operatorId = Guid.NewGuid();
            string token = cache.Store(operatorId);

            // Act
            Guid? result = cache.TryTake(token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(operatorId, result.Value);
        }

        [Fact]
        public void TryTake_IsSingleUse_CannotTakeTwice()
        {
            // Arrange
            var cache = new TwoFactorPendingAuthCache();
            var operatorId = Guid.NewGuid();
            string token = cache.Store(operatorId);

            // Act - first take succeeds
            Guid? first = cache.TryTake(token);
            // Second take should fail
            Guid? second = cache.TryTake(token);

            // Assert
            Assert.NotNull(first);
            Assert.Equal(operatorId, first.Value);
            Assert.Null(second);
        }

        [Fact]
        public void TryTake_WithInvalidToken_ReturnsNull()
        {
            // Arrange
            var cache = new TwoFactorPendingAuthCache();

            // Act
            Guid? result = cache.TryTake("invalid-token");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void TryTake_WithExpiredToken_ReturnsNull()
        {
            // Arrange
            var cache = new TwoFactorPendingAuthCache();
            var operatorId = Guid.NewGuid();
            string token = cache.Store(operatorId);

            // Act - wait for expiration (5 minutes + 1 second)
            System.Threading.Thread.Sleep(5 * 60 * 1000 + 1000);
            Guid? result = cache.TryTake(token);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void StoreMultiple_EachHasUniqueToken()
        {
            // Arrange
            var cache = new TwoFactorPendingAuthCache();
            var op1 = Guid.NewGuid();
            var op2 = Guid.NewGuid();

            // Act
            string token1 = cache.Store(op1);
            string token2 = cache.Store(op2);

            // Assert
            Assert.NotEqual(token1, token2);
            Assert.Equal(op1, cache.TryTake(token1));
            Assert.Equal(op2, cache.TryTake(token2));
        }
    }
}
