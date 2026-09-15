using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Data.Common.Tests;

public class UserAndAccountEntityTests
{
    [Fact]
    public void User_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        string providerUserKey = "ring:user123";
        string displayName = "John Doe";
        string email = "john@example.com";
        DateTime createdUtc = DateTime.UtcNow;

        // Act
        var user = new User
        {
            Id = id,
            ProviderUserKey = providerUserKey,
            DisplayName = displayName,
            Email = email,
            CreatedUtc = createdUtc
        };

        // Assert
        Assert.Equal(id, user.Id);
        Assert.Equal(providerUserKey, user.ProviderUserKey);
        Assert.Equal(displayName, user.DisplayName);
        Assert.Equal(email, user.Email);
        Assert.Equal(createdUtc, user.CreatedUtc);
    }

    [Fact]
    public void User_CreatedWithNullEmail_EmailIsNull()
    {
        // Arrange & Act
        var user = new User
        {
            Id = Guid.NewGuid(),
            ProviderUserKey = "ring:user123",
            DisplayName = "John Doe",
            Email = null,
            CreatedUtc = DateTime.UtcNow
        };

        // Assert
        Assert.Null(user.Email);
    }

    [Fact]
    public void ProviderAccount_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        string providerName = "Ring";
        DateTime linkedUtc = DateTime.UtcNow;
        DateTime lastSuccessfulAuthUtc = DateTime.UtcNow.AddHours(-1);
        bool isActive = true;

        // Act
        var providerAccount = new ProviderAccount
        {
            Id = id,
            UserId = userId,
            ProviderName = providerName,
            LinkedUtc = linkedUtc,
            LastSuccessfulAuthUtc = lastSuccessfulAuthUtc,
            IsActive = isActive
        };

        // Assert
        Assert.Equal(id, providerAccount.Id);
        Assert.Equal(userId, providerAccount.UserId);
        Assert.Equal(providerName, providerAccount.ProviderName);
        Assert.Equal(linkedUtc, providerAccount.LinkedUtc);
        Assert.Equal(lastSuccessfulAuthUtc, providerAccount.LastSuccessfulAuthUtc);
        Assert.Equal(isActive, providerAccount.IsActive);
    }

    [Fact]
    public void ProviderAccount_WithoutAuthHistory_LastSuccessfulAuthUtcIsNull()
    {
        // Arrange & Act
        var providerAccount = new ProviderAccount
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProviderName = "Ring",
            LinkedUtc = DateTime.UtcNow,
            LastSuccessfulAuthUtc = null,
            IsActive = false
        };

        // Assert
        Assert.Null(providerAccount.LastSuccessfulAuthUtc);
        Assert.False(providerAccount.IsActive);
    }
}
