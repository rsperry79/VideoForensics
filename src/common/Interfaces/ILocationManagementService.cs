#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Ring.Api.Entities;

namespace Ring.Api.Interfaces;

/// <summary>
/// Service for managing location settings and configurations.
/// </summary>
public interface ILocationManagementService
{
    /// <summary>
    /// Gets the current mode (Home, Away, Disarmed) for a location.
    /// </summary>
    Task<LocationMode> GetLocationMode(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the mode (Home, Away, Disarmed) for a location.
    /// </summary>
    Task<bool> SetLocationMode(Guid locationId, LocationMode mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users who have access to a location.
    /// </summary>
    Task<List<SharedUser>> GetSharedUsers(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending invitations for a location.
    /// </summary>
    Task<List<Invitation>> GetInvitations(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a location.
    /// </summary>
    Task<Location> GetLocationDetails(Guid locationId, CancellationToken cancellationToken = default);
}
