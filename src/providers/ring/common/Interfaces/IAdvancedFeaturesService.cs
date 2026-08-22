#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Interfaces;

/// <summary>
/// Service for advanced Ring device features.
/// </summary>
public interface IAdvancedFeaturesService
{
    /// <summary>
    /// Gets motion zones configured for a doorbot.
    /// </summary>
    Task<List<MotionZone>> GetMotionZones(string doorbotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates motion zones for a doorbot.
    /// </summary>
    Task<bool> UpdateMotionZones(string doorbotId, List<MotionZone> zones, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets currently active doorbells and motion detections.
    /// </summary>
    Task<List<DoorbotHistoryEvent>> GetActiveDings(CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlocks an intercom door.
    /// </summary>
    Task<bool> UnlockIntercom(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a live view session for a device.
    /// </summary>
    Task<System.Text.Json.JsonElement> GetLiveViewSession(string doorbotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets light group information.
    /// </summary>
    Task<System.Text.Json.JsonElement> GetLightGroups(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets advanced motion zone information.
    /// </summary>
    Task<System.Text.Json.JsonElement> GetAdvancedMotionSettings(string doorbotId, CancellationToken cancellationToken = default);
}
