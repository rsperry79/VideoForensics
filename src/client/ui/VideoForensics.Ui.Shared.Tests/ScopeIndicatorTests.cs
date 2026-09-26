namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Ui.Shared.Services.Scope;

public class ScopeIndicator_IsCustom_Tests
{
    [Fact]
    public void Default_Scope_Returns_False()
    {
        // Arrange
        var utcNow = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var defaultScope = ForensicScope.Default(utcNow);

        // Act
        var result = ScopeIndicator.IsCustom(defaultScope, utcNow);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Scope_With_Device_Selected_Returns_True()
    {
        // Arrange
        var utcNow = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var deviceId = Guid.NewGuid();
        var customScope = new ForensicScope(
            DeviceIds: new List<Guid> { deviceId },
            FromUtc: utcNow.Date.AddDays(-7),
            ToUtc: utcNow.Date.AddDays(1),
            Search: null);

        // Act
        var result = ScopeIndicator.IsCustom(customScope, utcNow);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Scope_With_Search_Returns_True()
    {
        // Arrange
        var utcNow = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var customScope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: utcNow.Date.AddDays(-7),
            ToUtc: utcNow.Date.AddDays(1),
            Search: "searchterm");

        // Act
        var result = ScopeIndicator.IsCustom(customScope, utcNow);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Scope_With_Different_Time_Window_Returns_True()
    {
        // Arrange
        var utcNow = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var customScope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: utcNow.Date.AddDays(-30),  // Different window
            ToUtc: utcNow.Date.AddDays(1),
            Search: null);

        // Act
        var result = ScopeIndicator.IsCustom(customScope, utcNow);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Scope_With_Different_End_Date_Returns_True()
    {
        // Arrange
        var utcNow = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var customScope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: utcNow.Date.AddDays(-7),
            ToUtc: utcNow.Date.AddDays(2),  // Different end
            Search: null);

        // Act
        var result = ScopeIndicator.IsCustom(customScope, utcNow);

        // Assert
        Assert.True(result);
    }
}
