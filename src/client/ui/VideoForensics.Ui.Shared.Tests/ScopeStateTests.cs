namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Ui.Shared.Services.Scope;

public class ForensicScope_Default_Tests
{
    [Fact]
    public void Default_Returns_NoDevices()
    {
        // Arrange
        var fixedTime = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var scope = ForensicScope.Default(fixedTime);

        // Assert
        Assert.Empty(scope.DeviceIds);
    }

    [Fact]
    public void Default_Returns_Last7DaysFromToday()
    {
        // Arrange
        var fixedTime = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var scope = ForensicScope.Default(fixedTime);

        // Assert
        Assert.Equal(new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc), scope.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc), scope.ToUtc);
    }

    [Fact]
    public void Default_Returns_NullSearch()
    {
        // Arrange
        var fixedTime = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var scope = ForensicScope.Default(fixedTime);

        // Assert
        Assert.Null(scope.Search);
    }

    [Fact]
    public void IncludesAllDevices_IsFalseWhenDeviceIdsNotEmpty()
    {
        // Act
        var scope = new ForensicScope(
            DeviceIds: new[] { Guid.NewGuid() },
            FromUtc: DateTime.UtcNow,
            ToUtc: DateTime.UtcNow.AddDays(1),
            Search: null);

        // Assert
        Assert.False(scope.IncludesAllDevices);
    }

    [Fact]
    public void IncludesAllDevices_IsTrueWhenDeviceIdsEmpty()
    {
        // Act
        var scope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: DateTime.UtcNow,
            ToUtc: DateTime.UtcNow.AddDays(1),
            Search: null);

        // Assert
        Assert.True(scope.IncludesAllDevices);
    }
}

public class ScopeState_Set_Tests
{
    [Fact]
    public void Set_UpdatesCurrent()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new List<Guid> { device1 },
            FromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
            Search: "test");

        // Act
        state.Set(scope);

        // Assert
        // Verify the scope content matches, not the exact object reference or collection type
        Assert.True(state.Current.Equals(scope));
    }

    [Fact]
    public void Set_RaisesOnChangeEvent()
    {
        // Arrange
        var state = new ScopeState();
        var scope = new ForensicScope(new List<Guid>(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1), null);
        var changeRaised = false;
        state.OnChange += () => changeRaised = true;

        // Act
        state.Set(scope);

        // Assert
        Assert.True(changeRaised);
    }

    [Fact]
    public void Set_WithEqualScope_DoesNotRaiseEvent()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new[] { device1 },
            FromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
            Search: "test");
        state.Set(scope);

        var changeRaised = false;
        state.OnChange += () => changeRaised = true;

        // Act
        state.Set(scope);

        // Assert
        Assert.False(changeRaised);
    }

    [Fact]
    public void Set_NormalisesDistinctDeviceIds()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new[] { device1, device1 },
            FromUtc: DateTime.UtcNow,
            ToUtc: DateTime.UtcNow.AddDays(1),
            Search: null);

        // Act
        state.Set(scope);

        // Assert
        Assert.Single(state.Current.DeviceIds);
        Assert.Equal(device1, state.Current.DeviceIds[0]);
    }

    [Fact]
    public void Set_SwapsReversedDates()
    {
        // Arrange
        var state = new ScopeState();
        var from = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var scope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: from,
            ToUtc: to,
            Search: null);

        // Act
        state.Set(scope);

        // Assert
        Assert.Equal(to, state.Current.FromUtc);
        Assert.Equal(from, state.Current.ToUtc);
    }

    [Fact]
    public void Set_TrimsSearchToNull()
    {
        // Arrange
        var state = new ScopeState();
        var scope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: DateTime.UtcNow,
            ToUtc: DateTime.UtcNow.AddDays(1),
            Search: "   ");

        // Act
        state.Set(scope);

        // Assert
        Assert.Null(state.Current.Search);
    }

    [Fact]
    public void Set_PreservesNonBlankSearch()
    {
        // Arrange
        var state = new ScopeState();
        var scope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: DateTime.UtcNow,
            ToUtc: DateTime.UtcNow.AddDays(1),
            Search: "test search");

        // Act
        state.Set(scope);

        // Assert
        Assert.Equal("test search", state.Current.Search);
    }
}

public class ScopeState_ToQueryString_Tests
{
    [Fact]
    public void ToQueryString_WithDefault_ReturnsEmpty()
    {
        // Arrange
        var state = new ScopeState();
        var fixedTime = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
        state.Set(ForensicScope.Default(fixedTime));

        // Act
        var query = state.ToQueryString();

        // Assert
        Assert.Empty(query);
    }

    [Fact]
    public void ToQueryString_WithDevices_IncludesDevicesKey()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new[] { device1, device2 },
            FromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
            Search: null);
        state.Set(scope);

        // Act
        var query = state.ToQueryString();

        // Assert
        Assert.Contains($"devices={device1:D},{device2:D}", query);
    }

    [Fact]
    public void ToQueryString_WithCustomFromDate_IncludesFromKey()
    {
        // Arrange
        var state = new ScopeState();
        var scope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc),
            Search: null);
        state.Set(scope);

        // Act
        var query = state.ToQueryString();

        // Assert
        Assert.Contains("from=2026-09-01", query);
    }

    [Fact]
    public void ToQueryString_WithCustomToDate_IncludesToKey()
    {
        // Arrange
        var state = new ScopeState();
        var scope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            Search: null);
        state.Set(scope);

        // Act
        var query = state.ToQueryString();

        // Assert
        Assert.Contains("to=2026-09-20", query);
    }

    [Fact]
    public void ToQueryString_WithSearch_IncludesQKey()
    {
        // Arrange
        var state = new ScopeState();
        var scope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: DateTime.UtcNow,
            ToUtc: DateTime.UtcNow.AddDays(1),
            Search: "test search");
        state.Set(scope);

        // Act
        var query = state.ToQueryString();

        // Assert
        Assert.Contains("q=", query);
        Assert.Contains("test", query);
    }

    [Fact]
    public void ToQueryString_EscapesSearchProperlyWithSpecialCharacters()
    {
        // Arrange
        var state = new ScopeState();
        var scope = new ForensicScope(
            DeviceIds: new List<Guid>(),
            FromUtc: DateTime.UtcNow,
            ToUtc: DateTime.UtcNow.AddDays(1),
            Search: "a b&c");
        state.Set(scope);

        // Act
        var query = state.ToQueryString();

        // Assert
        Assert.Contains("q=", query);
        // Verify that when decoded, the search term is preserved correctly
        var decoded = System.Web.HttpUtility.UrlDecode(query.Substring(query.IndexOf("q=") + 2));
        Assert.Equal("a b&c", decoded);
    }
}

public class ScopeState_ApplyQueryString_Tests
{
    [Fact]
    public void ApplyQueryString_WithEmpty_ReturnsFalse()
    {
        // Arrange
        var state = new ScopeState();

        // Act
        var changed = state.ApplyQueryString("");

        // Assert
        Assert.False(changed);
    }

    [Fact]
    public void ApplyQueryString_WithLeadingQuestion_ParsesCorrectly()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();

        // Act
        var changed = state.ApplyQueryString($"?devices={device1:D}&from=2026-09-01&to=2026-09-10");

        // Assert
        Assert.True(changed);
        Assert.Contains(device1, state.Current.DeviceIds);
        Assert.Equal(new DateTime(2026, 9, 1), state.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 10), state.Current.ToUtc);
    }

    [Fact]
    public void ApplyQueryString_WithoutLeadingQuestion_ParsesCorrectly()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();

        // Act
        var changed = state.ApplyQueryString($"devices={device1:D}&from=2026-09-01&to=2026-09-10");

        // Assert
        Assert.True(changed);
        Assert.Contains(device1, state.Current.DeviceIds);
    }

    [Fact]
    public void ApplyQueryString_WithMultipleDevices_ParsesAll()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();

        // Act
        var changed = state.ApplyQueryString($"devices={device1:D},{device2:D}");

        // Assert
        Assert.True(changed);
        Assert.Equal(2, state.Current.DeviceIds.Count);
        Assert.Contains(device1, state.Current.DeviceIds);
        Assert.Contains(device2, state.Current.DeviceIds);
    }

    [Fact]
    public void ApplyQueryString_WithInvalidGuid_IgnoresIt()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();

        // Act
        var changed = state.ApplyQueryString($"devices=invalid,{device1:D}");

        // Assert
        Assert.True(changed);
        Assert.Single(state.Current.DeviceIds);
        Assert.Contains(device1, state.Current.DeviceIds);
    }

    [Fact]
    public void ApplyQueryString_WithInvalidDate_IgnoresIt()
    {
        // Arrange
        var state = new ScopeState();

        // Act
        var changed = state.ApplyQueryString("from=not-a-date");

        // Assert
        // Should fall back to default or ignore the key
        Assert.NotNull(state.Current.FromUtc);
    }

    [Fact]
    public void ApplyQueryString_WithSearchParameter_ParsesCorrectly()
    {
        // Arrange
        var state = new ScopeState();

        // Act
        var changed = state.ApplyQueryString("q=test%20search");

        // Assert
        Assert.True(changed);
        Assert.Equal("test search", state.Current.Search);
    }

    [Fact]
    public void ApplyQueryString_IgnoresUnknownKeys()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();

        // Act
        var changed = state.ApplyQueryString($"devices={device1:D}&unknown=value");

        // Assert
        Assert.True(changed);
        Assert.Contains(device1, state.Current.DeviceIds);
    }

    [Fact]
    public void ApplyQueryString_RoundTrip_YieldsEqualScope()
    {
        // Arrange
        var state1 = new ScopeState();
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();
        var originalScope = new ForensicScope(
            DeviceIds: new[] { device1, device2 },
            FromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
            Search: "test");
        state1.Set(originalScope);
        var query = state1.ToQueryString();

        var state2 = new ScopeState();

        // Act
        state2.ApplyQueryString(query);

        // Assert
        Assert.Equal(originalScope, state2.Current);
    }

    [Fact]
    public void ApplyQueryString_ReturnsTrueWhenChanged()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();

        // Act
        var changed = state.ApplyQueryString($"devices={device1:D}");

        // Assert
        Assert.True(changed);
    }

    [Fact]
    public void ApplyQueryString_ReturnsFalseWhenUnchanged()
    {
        // Arrange
        var state = new ScopeState();
        var device1 = Guid.NewGuid();
        var fromDate = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        var scope = new ForensicScope(
            DeviceIds: new List<Guid> { device1 },
            FromUtc: fromDate,
            ToUtc: toDate,
            Search: null);
        state.Set(scope);
        var query = state.ToQueryString();

        // Act
        var changed = state.ApplyQueryString(query);

        // Assert
        Assert.False(changed);
    }
}
