using Microsoft.Extensions.Logging;

using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Evidence;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>What happened to a tag request.</summary>
public enum TagOutcome
{
    /// <summary>A case was open, so the item was pinned to it now.</summary>
    PinnedToOpenCase,

    /// <summary>No case was open, so the tag waits in memory for one.</summary>
    KeptPending,

    /// <summary>The item was already pending, or the open case already holds it.</summary>
    AlreadyTagged,

    /// <summary>The pin was refused. Nothing was stored.</summary>
    Failed,
}

/// <summary>
/// The review page's tag action. Pins straight to the open case when there is one; otherwise keeps the tag
/// in <see cref="PendingEvidenceTags"/> until a case opens.
/// </summary>
public class ReviewEvidenceTagger
{
    private readonly CaseState _caseState;
    private readonly PendingEvidenceTags _pending;
    private readonly PairedSessionState _session;
    private readonly ILogger<ReviewEvidenceTagger>? _logger;

    /// <summary>Creates the tagger. The logger is optional so DI can omit it.</summary>
    public ReviewEvidenceTagger(
        CaseState caseState,
        PendingEvidenceTags pending,
        PairedSessionState session,
        ILogger<ReviewEvidenceTagger>? logger = null)
    {
        _caseState = caseState;
        _pending = pending;
        _session = session;
        _logger = logger;
    }

    /// <summary>True when the item is waiting in memory for a case to open.</summary>
    public bool IsPending(CaseItemKind kind, Guid targetId)
    {
        return _pending.Items.Any(i => i.Kind == kind && i.TargetId == targetId);
    }

    /// <summary>
    /// Tags an evidence item. The actor is the paired operator; a session without one is recorded as "unknown",
    /// the same fallback the inspector uses.
    /// </summary>
    public async Task<TagOutcome> TagAsync(
        CaseItemKind kind,
        Guid targetId,
        Guid deviceId,
        DateTime? occurredAtUtc,
        string reason,
        CancellationToken ct)
    {
        string actor = EvidenceActions.ResolveOperatorActor(_session.OperatorId) ?? "unknown";

        // No open case: keep the tag in memory. The pending list is what makes tagging work before a case exists.
        if (_caseState.ActiveCase is null)
        {
            var tag = new PendingTag(kind, targetId, deviceId, occurredAtUtc, reason, actor);
            return _pending.Add(tag) ? TagOutcome.KeptPending : TagOutcome.AlreadyTagged;
        }

        try
        {
            if (await _caseState.HasActivePinAsync(kind, targetId, ct))
            {
                return TagOutcome.AlreadyTagged;
            }

            _ = await _caseState.PinAsync(kind, targetId, reason, actor, ct);
            return TagOutcome.PinnedToOpenCase;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(
                "{Kind} tag could not be pinned to the open case: {ExceptionType}: {Message}",
                kind,
                ex.GetType().Name,
                ex.Message);
            return TagOutcome.Failed;
        }
    }
}
