using Microsoft.Extensions.Logging;

using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// An evidence item tagged on the review page. Held in memory only; lost when the session or circuit restarts.
/// </summary>
/// <param name="Kind">Whether the tag is an event or a downloaded media item.</param>
/// <param name="TargetId">The event or media item id.</param>
/// <param name="DeviceId">The camera the item came from, used for display.</param>
/// <param name="OccurredAtUtc">When the item happened, when known.</param>
/// <param name="Reason">The reason stored on the pin when the tag attaches.</param>
/// <param name="Actor">The operator who tagged the item.</param>
public sealed record PendingTag(
    CaseItemKind Kind,
    Guid TargetId,
    Guid DeviceId,
    DateTime? OccurredAtUtc,
    string Reason,
    string Actor);

/// <summary>
/// The result of attaching pending tags to the open case.
/// </summary>
/// <param name="Attached">Items the case now holds: newly pinned, or already pinned there.</param>
/// <param name="Failed">Items kept pending because their pin failed.</param>
/// <param name="AtUtc">When the attach happened. Null when nothing attached.</param>
public sealed record AttachResult(int Attached, int Failed, DateTime? AtUtc = null);

/// <summary>
/// Scoped, in-memory list of evidence tags made while no case is open. When a case becomes active,
/// <see cref="AttachToActiveCaseAsync"/> pins them to it. Nothing here survives a session or circuit restart.
/// </summary>
/// <remarks>
/// The list is guarded by a lock because a tag and a case activation can arrive on different threads.
/// Events are raised outside the lock so a handler can read the list without deadlocking.
/// </remarks>
public class PendingEvidenceTags
{
    private readonly object _gate = new();
    private readonly List<PendingTag> _items = [];
    private readonly ILogger<PendingEvidenceTags>? _logger;
    private AttachResult? _lastAttach;

    /// <summary>Creates an empty list. The logger is optional so DI can omit it.</summary>
    public PendingEvidenceTags(ILogger<PendingEvidenceTags>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>Raised once per call that changes the list.</summary>
    public event Action? OnChange;

    /// <summary>Raised after an attach that pinned at least one item.</summary>
    public event Action<AttachResult>? Attached;

    /// <summary>A snapshot of the pending tags, in the order they were added.</summary>
    public IReadOnlyList<PendingTag> Items
    {
        get
        {
            lock (_gate)
            {
                return _items.ToList();
            }
        }
    }

    /// <summary>The number of pending tags.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    /// <summary>The most recent attach that pinned at least one item, or null.</summary>
    public AttachResult? LastAttach
    {
        get
        {
            lock (_gate)
            {
                return _lastAttach;
            }
        }
    }

    /// <summary>
    /// Adds a tag. Returns false, and changes nothing, when the same kind and target is already pending.
    /// </summary>
    public bool Add(PendingTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        lock (_gate)
        {
            if (_items.Any(i => i.Kind == tag.Kind && i.TargetId == tag.TargetId))
            {
                return false;
            }

            _items.Add(tag);
        }

        OnChange?.Invoke();
        return true;
    }

    /// <summary>Removes the tag for the given kind and target. Returns false when none was pending.</summary>
    public bool Remove(CaseItemKind kind, Guid targetId)
    {
        bool removed;
        lock (_gate)
        {
            removed = _items.RemoveAll(i => i.Kind == kind && i.TargetId == targetId) > 0;
        }

        if (removed)
        {
            OnChange?.Invoke();
        }

        return removed;
    }

    /// <summary>Drops every pending tag.</summary>
    public void Clear()
    {
        bool hadItems;
        lock (_gate)
        {
            hadItems = _items.Count > 0;
            _items.Clear();
        }

        if (hadItems)
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>
    /// Pins every pending tag to the active case, in the order added. A tag leaves the list when its pin
    /// succeeds or when the case already holds it. A tag whose pin fails stays pending, and the rest still attach.
    /// Returns zero counts and changes nothing when no case is active.
    /// </summary>
    /// <remarks>
    /// The duplicate check runs before each pin because the repository reports an existing pin as a generic
    /// InvalidOperationException, which cannot be told apart from other refusals after the fact.
    /// </remarks>
    public virtual async Task<AttachResult> AttachToActiveCaseAsync(CaseState caseState, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caseState);
        if (caseState.ActiveCase is null)
        {
            return new AttachResult(0, 0);
        }

        var attachedTags = new List<PendingTag>();
        int failed = 0;
        foreach (PendingTag tag in Items)
        {
            try
            {
                if (!await caseState.HasActivePinAsync(tag.Kind, tag.TargetId, ct))
                {
                    _ = await caseState.PinAsync(tag.Kind, tag.TargetId, tag.Reason, tag.Actor, ct);
                }

                attachedTags.Add(tag);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                _logger?.LogWarning(
                    "Pending {Kind} tag could not be attached to the open case: {ExceptionType}: {Message}",
                    tag.Kind,
                    ex.GetType().Name,
                    ex.Message);
            }
        }

        if (attachedTags.Count == 0)
        {
            return new AttachResult(0, failed);
        }

        lock (_gate)
        {
            foreach (PendingTag tag in attachedTags)
            {
                _ = _items.RemoveAll(i => i.Kind == tag.Kind && i.TargetId == tag.TargetId);
            }
        }

        var result = new AttachResult(attachedTags.Count, failed, DateTime.UtcNow);
        lock (_gate)
        {
            _lastAttach = result;
        }

        OnChange?.Invoke();
        Attached?.Invoke(result);
        return result;
    }
}
