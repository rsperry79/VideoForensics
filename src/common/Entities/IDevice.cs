using System;

namespace KoenZomers.Ring.Api.Entities;

/// <summary>
/// Represents a Ring device (camera, chime, etc.).
/// </summary>
public interface IDevice
{
    /// <summary>
    /// Gets the unique identifier of the device.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the display name of the device.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the type of device (e.g., "doorbot", "chime").
    /// </summary>
    string DeviceType { get; }

    /// <summary>
    /// Gets the location ID this device belongs to.
    /// </summary>
    Guid LocationId { get; }

    /// <summary>
    /// Gets whether the device is currently online.
    /// </summary>
    bool IsOnline { get; }

    /// <summary>
    /// Gets when the device was created/added.
    /// </summary>
    DateTime CreatedAt { get; }
}
