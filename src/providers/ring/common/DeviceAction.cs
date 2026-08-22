using System.Collections.Generic;

namespace VideoForensics.Providers.Ring.Entities;

/// <summary>
/// Represents an action to perform on a device.
/// </summary>
public class DeviceAction
{
    /// <summary>
    /// The type of action to perform (e.g., "light_on", "siren_off").
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Additional parameters for the action.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}
