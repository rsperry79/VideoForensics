using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Time;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services;

public class ThemePreferenceServiceTests
{
    [Fact]
    public async Task InitializeAsync_NoSignedInOperatorStoredDark_UsesLocalCache()
    {
        // Arrange - the device remembers Dark from the last session; no operator is signed in yet.
        var js = new ThemeJSRuntime("Dark");
        ThemePreferenceService service = CreateService(js, operatorId: null, savedMode: null);

        // Act
        await service.InitializeAsync();

        // Assert - first paint matches the last session instead of falling back to System.
        Assert.Equal(ThemeMode.Dark, service.Mode);
        Assert.True(service.EffectiveIsDark);
    }

    [Fact]
    public async Task InitializeAsync_NothingStored_DefaultsToSystem()
    {
        var js = new ThemeJSRuntime(storedMode: null);
        ThemePreferenceService service = CreateService(js, operatorId: null, savedMode: null);

        await service.InitializeAsync();

        Assert.Equal(ThemeMode.System, service.Mode);
    }

    [Fact]
    public async Task InitializeAsync_SignedInOperatorDiffersFromLocalCache_ServerWinsAndRefreshesLocalCache()
    {
        // Arrange - local cache says Dark, but the operator's stored row says Light.
        var operatorId = Guid.NewGuid();
        var js = new ThemeJSRuntime("Dark");
        ThemePreferenceService service = CreateService(js, operatorId, savedMode: "Light");

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.Equal(ThemeMode.Light, service.Mode);
        Assert.Equal("Light", js.StoredMode);
    }

    [Fact]
    public async Task InitializeAsync_SignedInOperatorMatchesLocalCache_DoesNotRewriteLocalCache()
    {
        var operatorId = Guid.NewGuid();
        var js = new ThemeJSRuntime("Dark");
        ThemePreferenceService service = CreateService(js, operatorId, savedMode: "Dark");

        await service.InitializeAsync();

        Assert.Equal(ThemeMode.Dark, service.Mode);
        Assert.Equal(0, js.StoreWrites);
    }

    [Fact]
    public async Task InitializeAsync_SignedInOperatorNoSavedRow_KeepsLocalCache()
    {
        var operatorId = Guid.NewGuid();
        var js = new ThemeJSRuntime("Light");
        ThemePreferenceService service = CreateService(js, operatorId, savedMode: null);

        await service.InitializeAsync();

        Assert.Equal(ThemeMode.Light, service.Mode);
        Assert.Equal("Light", js.StoredMode);
    }

    [Fact]
    public async Task SetModeAsync_NoSignedInOperator_WritesLocalCache()
    {
        // Arrange - the choice cannot reach the database without an operator, but it must still be remembered on this device.
        var js = new ThemeJSRuntime(storedMode: null);
        ThemePreferenceService service = CreateService(js, operatorId: null, savedMode: null);
        await service.InitializeAsync();

        // Act
        await service.SetModeAsync(ThemeMode.Dark);

        // Assert
        Assert.Equal("Dark", js.StoredMode);
    }

    [Fact]
    public async Task InitializeAsync_SignedInOperatorTimeFormatDiffersFromLocalCache_ServerWins()
    {
        // Arrange - the device last used 24-hour time, but the operator's row says 12-hour.
        var operatorId = Guid.NewGuid();
        var js = new ThemeJSRuntime("Light") { StoredTimeFormat = "TwentyFourHour" };
        ThemePreferenceService service = CreateService(js, operatorId, savedMode: "Light", savedTimeFormat: "TwelveHour");

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.Equal(TimeFormatPreference.TwelveHour, service.TimeFormat);
        Assert.Equal("TwelveHour", js.StoredTimeFormat);
    }

    [Fact]
    public async Task InitializeAsync_NoSignedInOperator_UsesTheDeviceTimeFormat()
    {
        var js = new ThemeJSRuntime("Light") { StoredTimeFormat = "TwentyFourHour" };
        ThemePreferenceService service = CreateService(js, operatorId: null, savedMode: null);

        await service.InitializeAsync();

        Assert.Equal(TimeFormatPreference.TwentyFourHour, service.TimeFormat);
    }

    [Fact]
    public async Task SetTimeFormatAsync_SignedIn_StoresOnTheOperatorRowAndKeepsTheTheme()
    {
        // Arrange - the operator is on Dark with 12-hour time; they switch to 24-hour.
        var operatorId = Guid.NewGuid();
        var js = new ThemeJSRuntime("Dark");
        var repository = new Mock<IOperatorPreferencesRepository>();
        _ = repository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperatorPreferences { OperatorId = operatorId, ThemeMode = "Dark", UiMode = "Standard", TimeFormat = "TwelveHour" });
        OperatorPreferences? written = null;
        _ = repository
            .Setup(r => r.UpsertAsync(It.IsAny<OperatorPreferences>(), It.IsAny<CancellationToken>()))
            .Callback<OperatorPreferences, CancellationToken>((p, _) => written = p)
            .Returns(Task.CompletedTask);
        var session = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = session.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = session.Setup(s => s.OperatorId).Returns(operatorId);
        var service = new ThemePreferenceService(js, repository.Object, session.Object, Mock.Of<ICultureSwitcher>());
        await service.InitializeAsync();
        var changes = 0;
        service.OnChange += () => changes++;

        // Act
        await service.SetTimeFormatAsync(TimeFormatPreference.TwentyFourHour);

        // Assert
        Assert.Equal(TimeFormatPreference.TwentyFourHour, service.TimeFormat);
        Assert.Equal("TwentyFourHour", js.StoredTimeFormat);
        Assert.NotNull(written);
        Assert.Equal("TwentyFourHour", written.TimeFormat);
        Assert.Equal("Dark", written.ThemeMode);
        Assert.Equal(1, changes);
    }

    private static ThemePreferenceService CreateService(ThemeJSRuntime js, Guid? operatorId, string? savedMode, string? savedTimeFormat = null)
    {
        var repository = new Mock<IOperatorPreferencesRepository>();
        _ = repository
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedMode is null
                ? null
                : new OperatorPreferences { OperatorId = operatorId!.Value, ThemeMode = savedMode, UiMode = "Standard", TimeFormat = savedTimeFormat ?? "Locale" });

        var session = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = session.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = session.Setup(s => s.OperatorId).Returns(operatorId);

        return new ThemePreferenceService(js, repository.Object, session.Object, Mock.Of<ICultureSwitcher>());
    }

    /// <summary>
    /// Answers the theme interop calls from an in-memory value, so the stored mode can be read back after a call.
    /// Every other call completes with its default result.
    /// </summary>
    private sealed class ThemeJSRuntime(string? storedMode) : IJSRuntime
    {
        public string? StoredMode { get; private set; } = storedMode;

        public int StoreWrites { get; private set; }

        public string? StoredTimeFormat { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            switch (identifier)
            {
                case "vfTheme.getStoredMode":
                    return ValueTask.FromResult((TValue)(object?)StoredMode!);
                case "vfTheme.setStoredMode":
                    StoredMode = (string)args![0]!;
                    StoreWrites++;
                    return ValueTask.FromResult(default(TValue)!);
                case "vfTheme.getStoredTimeFormat":
                    return ValueTask.FromResult((TValue)(object?)StoredTimeFormat!);
                case "vfTheme.setStoredTimeFormat":
                    StoredTimeFormat = (string)args![0]!;
                    return ValueTask.FromResult(default(TValue)!);
                case "vfTheme.getPrefersDark":
                    return ValueTask.FromResult((TValue)(object)false);
                default:
                    return ValueTask.FromResult(default(TValue)!);
            }
        }
    }
}
