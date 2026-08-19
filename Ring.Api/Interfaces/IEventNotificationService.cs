#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// Service for managing event subscriptions and notifications.
/// </summary>
public interface IEventNotificationService
{
    /// <summary>
    /// Gets all event subscriptions for the user.
    /// </summary>
    Task<List<object>> GetEventSubscriptions();

    /// <summary>
    /// Updates event subscriptions.
    /// </summary>
    Task<bool> UpdateEventSubscriptions(List<object> subscriptions);

    /// <summary>
    /// Gets all events for a location.
    /// </summary>
    Task<List<LocationEvent>> GetLocationEvents(Guid locationId);

    /// <summary>
    /// Gets events with advanced filtering.
    /// </summary>
    Task<List<HistoryEvent>> GetEvents(
        int limit = 100,
        DateTimeOffset? dateRange = null,
        string? kind = null);
}
