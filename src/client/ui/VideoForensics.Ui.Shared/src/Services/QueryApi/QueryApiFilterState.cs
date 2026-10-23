using VideoForensics.Client.Ui.Shared.Contracts;

namespace VideoForensics.Client.Ui.Shared.Services.QueryApi;

/// <summary>
/// Default <see cref="IQueryApiFilterState"/>. Registered scoped (one instance per circuit), like <see cref="Scope.ScopeState"/>.
/// </summary>
public sealed class QueryApiFilterState : IQueryApiFilterState
{
    /// <inheritdoc />
    public string? LocationId { get; private set; }

    /// <inheritdoc />
    public string? DeviceId { get; private set; }

    /// <inheritdoc />
    public event Action? OnChange;

    /// <inheritdoc />
    public void SetLocationId(string? locationId)
    {
        if (LocationId == locationId)
        {
            return;
        }

        LocationId = locationId;
        OnChange?.Invoke();
    }

    /// <inheritdoc />
    public void SetDeviceId(string? deviceId)
    {
        if (DeviceId == deviceId)
        {
            return;
        }

        DeviceId = deviceId;
        OnChange?.Invoke();
    }
}
