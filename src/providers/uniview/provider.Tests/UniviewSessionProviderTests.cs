using Xunit;

namespace VideoForensics.Providers.Uniview.Tests
{
    /// <summary>
    /// Tests for UniviewSessionProvider - in-memory session store for authenticated UniviewClient instances.
    /// No mocking needed; all logic is synchronous and stateful.
    /// </summary>
    public class UniviewSessionProviderTests
    {
        [Fact]
        public void UniviewSessionProvider_SetClientParameterless_StoresAndRetrieves()
        {
            // Arrange
            var provider = new UniviewSessionProvider();
            var client = new UniviewClient("192.168.1.1", "admin", "password");

            // Act
            provider.SetClient(client);
            UniviewClient? retrieved = provider.GetClient();

            // Assert
            Assert.NotNull(retrieved);
            Assert.Same(client, retrieved);
        }

        [Fact]
        public void UniviewSessionProvider_GetClientParameterless_ReturnsNullWhenNotSet()
        {
            // Arrange
            var provider = new UniviewSessionProvider();

            // Act
            UniviewClient? retrieved = provider.GetClient();

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public void UniviewSessionProvider_ClearClientParameterless_RemovesLastSetClient()
        {
            // Arrange
            var provider = new UniviewSessionProvider();
            var client = new UniviewClient("192.168.1.1", "admin", "password");
            provider.SetClient(client);

            // Act
            provider.ClearClient();
            UniviewClient? retrieved = provider.GetClient();

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public void UniviewSessionProvider_SetClientWithGuid_StoresAndRetrievesByGuid()
        {
            // Arrange
            var provider = new UniviewSessionProvider();
            var accountId = Guid.NewGuid();
            var client = new UniviewClient("192.168.1.1", "admin", "password");

            // Act
            provider.SetClient(accountId, client);
            UniviewClient? retrieved = provider.GetClient(accountId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Same(client, retrieved);
        }

        [Fact]
        public void UniviewSessionProvider_GetClientWithGuid_ReturnsNullForUnknownAccount()
        {
            // Arrange
            var provider = new UniviewSessionProvider();
            var unknownId = Guid.NewGuid();

            // Act
            UniviewClient? retrieved = provider.GetClient(unknownId);

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public void UniviewSessionProvider_ClearClientWithGuid_RemovesSpecificAccount()
        {
            // Arrange
            var provider = new UniviewSessionProvider();
            var accountId = Guid.NewGuid();
            var client = new UniviewClient("192.168.1.1", "admin", "password");
            provider.SetClient(accountId, client);

            // Act
            provider.ClearClient(accountId);
            UniviewClient? retrieved = provider.GetClient(accountId);

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public void UniviewSessionProvider_MultipleAccounts_TracksSeparately()
        {
            // Arrange
            var provider = new UniviewSessionProvider();
            var account1 = Guid.NewGuid();
            var account2 = Guid.NewGuid();
            var client1 = new UniviewClient("192.168.1.1", "admin", "password");
            var client2 = new UniviewClient("192.168.1.2", "admin", "password");

            // Act
            provider.SetClient(account1, client1);
            provider.SetClient(account2, client2);

            // Assert
            Assert.Same(client1, provider.GetClient(account1));
            Assert.Same(client2, provider.GetClient(account2));
        }

        [Fact]
        public void UniviewSessionProvider_SetParameterlessUpdatesLastSetTracking()
        {
            // Arrange
            var provider = new UniviewSessionProvider();
            var account1 = Guid.NewGuid();
            var client1 = new UniviewClient("192.168.1.1", "admin", "password");
            var clientDefault = new UniviewClient("192.168.1.3", "admin", "password");

            // Act
            provider.SetClient(account1, client1); // Sets via GUID
            provider.SetClient(clientDefault);     // Sets default (parameterless)

            // Assert - parameterless get should return the default
            UniviewClient? parameterlessRetrieved = provider.GetClient();
            Assert.Same(clientDefault, parameterlessRetrieved);

            // GUID-specific retrieval still works
            Assert.Same(client1, provider.GetClient(account1));
        }

        [Fact]
        public void UniviewSessionProvider_ClearParameterlessClearsOnlyLastSet()
        {
            // Arrange
            var provider = new UniviewSessionProvider();
            var account1 = Guid.NewGuid();
            var client1 = new UniviewClient("192.168.1.1", "admin", "password");
            var clientDefault = new UniviewClient("192.168.1.3", "admin", "password");

            provider.SetClient(account1, client1);
            provider.SetClient(clientDefault); // Now this is "last set"

            // Act
            provider.ClearClient(); // Clears the default (last set)

            // Assert - parameterless get returns null since last set was cleared
            Assert.Null(provider.GetClient());

            // But the other account's client is still there
            Assert.Same(client1, provider.GetClient(account1));
        }

        [Fact]
        public void UniviewSessionProvider_SetClient_ThrowsOnNull()
        {
            // Arrange
            var provider = new UniviewSessionProvider();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => provider.SetClient(Guid.NewGuid(), null!));
            _ = Assert.Throws<ArgumentNullException>(() => provider.SetClient(null!));
        }

        [Fact]
        public void UniviewSessionProvider_ClearingNonExistentAccount_IsIdempotent()
        {
            // Arrange
            var provider = new UniviewSessionProvider();
            var unknownId = Guid.NewGuid();

            // Act & Assert - should not throw
            provider.ClearClient(unknownId);
            provider.ClearClient(unknownId);
        }

        [Fact]
        public void UniviewSessionProvider_DefaultKeyAndGuidSetInteraction()
        {
            // Arrange
            var provider = new UniviewSessionProvider();
            Guid defaultKey = Guid.Empty; // The well-known default key
            var client1 = new UniviewClient("192.168.1.1", "admin", "password");

            _ = new UniviewClient("192.168.1.2", "admin", "password");

            // Act - set via parameterless (should use Guid.Empty internally)
            provider.SetClient(client1);

            // Then set via the default key explicitly
            UniviewClient? explicit1 = provider.GetClient(defaultKey);
            Assert.Same(client1, explicit1);

            // Now clear via GUID.Empty should clear the parameterless too
            provider.ClearClient(defaultKey);
            UniviewClient? afterClear = provider.GetClient();
            Assert.Null(afterClear);

            // And GUID.Empty retrieval returns null too
            Assert.Null(provider.GetClient(defaultKey));
        }
    }
}
