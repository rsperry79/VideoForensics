using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Data.Common.Tests;

public class CredentialEntityTests
{
    [Fact]
    public void Credential_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var providerAccountId = Guid.NewGuid();
        var credentialType = "Password";
        var encryptedValue = "encrypted_base64_value";
        var encryptionProvider = "DPAPI";
        var createdUtc = DateTime.UtcNow;
        var rotatedUtc = DateTime.UtcNow.AddDays(-7);

        // Act
        var credential = new Credential
        {
            Id = id,
            ProviderAccountId = providerAccountId,
            CredentialType = credentialType,
            EncryptedValue = encryptedValue,
            EncryptionProvider = encryptionProvider,
            CreatedUtc = createdUtc,
            RotatedUtc = rotatedUtc
        };

        // Assert
        Assert.Equal(id, credential.Id);
        Assert.Equal(providerAccountId, credential.ProviderAccountId);
        Assert.Equal(credentialType, credential.CredentialType);
        Assert.Equal(encryptedValue, credential.EncryptedValue);
        Assert.Equal(encryptionProvider, credential.EncryptionProvider);
        Assert.Equal(createdUtc, credential.CreatedUtc);
        Assert.Equal(rotatedUtc, credential.RotatedUtc);
    }

    [Fact]
    public void Credential_WithRefreshTokenType_RoundsTrip()
    {
        // Arrange & Act
        var credential = new Credential
        {
            Id = Guid.NewGuid(),
            ProviderAccountId = Guid.NewGuid(),
            CredentialType = "RefreshToken",
            EncryptedValue = "encrypted_token",
            EncryptionProvider = "AES",
            CreatedUtc = DateTime.UtcNow,
            RotatedUtc = null
        };

        // Assert
        Assert.Equal("RefreshToken", credential.CredentialType);
        Assert.Equal("AES", credential.EncryptionProvider);
        Assert.Null(credential.RotatedUtc);
    }

    [Fact]
    public void Credential_NeverRotated_RotatedUtcIsNull()
    {
        // Arrange & Act
        var credential = new Credential
        {
            Id = Guid.NewGuid(),
            ProviderAccountId = Guid.NewGuid(),
            CredentialType = "Password",
            EncryptedValue = "initial_encrypted",
            EncryptionProvider = "DPAPI",
            CreatedUtc = DateTime.UtcNow,
            RotatedUtc = null
        };

        // Assert
        Assert.Null(credential.RotatedUtc);
    }

    [Fact]
    public void Credential_WithDifferentEncryptionProviders_Distinguishes()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var providerAccountId = Guid.NewGuid();

        // Act
        var credentialDpapi = new Credential
        {
            Id = id1,
            ProviderAccountId = providerAccountId,
            CredentialType = "Password",
            EncryptedValue = "dpapi_value",
            EncryptionProvider = "DPAPI",
            CreatedUtc = DateTime.UtcNow,
            RotatedUtc = null
        };

        var credentialAes = new Credential
        {
            Id = id2,
            ProviderAccountId = providerAccountId,
            CredentialType = "Password",
            EncryptedValue = "aes_value",
            EncryptionProvider = "AES",
            CreatedUtc = DateTime.UtcNow,
            RotatedUtc = null
        };

        // Assert
        Assert.NotEqual(credentialDpapi.EncryptionProvider, credentialAes.EncryptionProvider);
    }
}
