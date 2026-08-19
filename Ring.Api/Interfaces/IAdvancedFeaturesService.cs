using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;
using KoenZomers.Ring.Api;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// Service for advanced Ring device features.
/// </summary>
public interface IAdvancedFeaturesService
{
    /// <summary>
    /// Gets motion zones configured for a doorbot.
    /// </summary>
    Task<List<MotionZone>> GetMotionZones(string doorbotId);

    /// <summary>
    /// Updates motion zones for a doorbot.
    /// </summary>
    Task<bool> UpdateMotionZones(string doorbotId, List<MotionZone> zones);

    /// <summary>
    /// Gets currently active doorbells and motion detections.
    /// </summary>
    Task<List<object>> GetActiveDings();

    /// <summary>
    /// Unlocks an intercom door.
    /// </summary>
    Task<bool> UnlockIntercom(string deviceId);

    /// <summary>
    /// Gets a live view session for a device.
    /// </summary>
    Task<object> GetLiveViewSession(string doorbotId);

    /// <summary>
    /// Gets light group information.
    /// </summary>
    Task<List<object>> GetLightGroups(Guid locationId);

    /// <summary>
    /// Gets advanced motion zone information.
    /// </summary>
    Task<Dictionary<string, object>> GetAdvancedMotionSettings(string doorbotId);
}
