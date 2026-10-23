namespace VideoForensics.Client.Ui.Shared.Contracts;

/// <summary>
/// Circuit-scoped selection shared by the Query API subpages (Devices, Events) so a chosen location or device
/// survives navigation between them. Setters raise <see cref="OnChange"/> only when the value actually changes.
/// </summary>
public interface IQueryApiFilterState
{
    /// <summary>The selected location id, or null when none is selected.</summary>
    string? LocationId { get; }

    /// <summary>The selected device id for device events, or null when none is selected.</summary>
    string? DeviceId { get; }

    /// <summary>Raised after <see cref="SetLocationId"/> or <see cref="SetDeviceId"/> changes a value.</summary>
    event Action? OnChange;

    /// <summary>Sets the selected location id and raises <see cref="OnChange"/> if it changed.</summary>
    void SetLocationId(string? locationId);

    /// <summary>Sets the selected device id and raises <see cref="OnChange"/> if it changed.</summary>
    void SetDeviceId(string? deviceId);
}
