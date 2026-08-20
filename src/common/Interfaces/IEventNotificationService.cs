#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Ring.Api.Entities;

namespace Ring.Api.Interfaces;

/// <summary>
/// Service for managing event subscriptions and notifications.
/// </summary>
public interface IEventNotificationService
{
    /// <summary>
    /// Gets all event subscriptions for the user. Returns a JsonElement representing the subscription structure.
    /// </summary>
    Task<System.Text.Json.JsonElement> GetEventSubscriptions(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates event subscriptions.
    /// </summary>
    Task<bool> UpdateEventSubscriptions(System.Text.Json.JsonElement subscriptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all events for a location.
    /// </summary>
    Task<List<LocationEvent>> GetLocationEvents(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events with advanced filtering.
    /// </summary>
    Task<List<HistoryEvent>> GetEvents(
        int limit = 100,
        DateTimeOffset? dateRange = null,
        string? kind = null,
        CancellationToken cancellationToken = default);
}
