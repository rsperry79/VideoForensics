using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Scope;

namespace VideoForensics.Ui.Shared.Services.Cases;

/// <summary>
/// Circuit-scoped state for the active forensic case and its scope management.
/// Tracks the currently-selected case and provides methods to activate, deactivate, and pin evidence items.
/// </summary>
public class CaseState
{
    private readonly ICaseRepository _caseRepository;
    private readonly ScopeState _scopeState;

    /// <summary>
    /// The currently-active case, or null if no case is selected.
    /// </summary>
    public ForensicCase? ActiveCase { get; private set; }

    /// <summary>
    /// The device IDs associated with the active case, if any.
    /// </summary>
    public IReadOnlyList<Guid> ActiveCaseDeviceIds { get; private set; } = new List<Guid>();

    /// <summary>
    /// Raised when the active case changes or is deactivated.
    /// </summary>
    public event Action? OnChange;

    public CaseState(ICaseRepository caseRepository, ScopeState scopeState)
    {
        _caseRepository = caseRepository;
        _scopeState = scopeState;
    }

    /// <summary>
    /// Activates a case by ID, loading its scope and applying it to the current scope state.
    /// If the case is not found, returns false and does not change the active case.
    /// </summary>
    public async Task<bool> ActivateAsync(Guid caseId, CancellationToken ct)
    {
        var forensicCase = await _caseRepository.GetAsync(caseId, ct);
        if (forensicCase is null)
        {
            return false;
        }

        var deviceIds = await _caseRepository.GetDeviceIdsAsync(caseId, ct);
        ActiveCase = forensicCase;
        ActiveCaseDeviceIds = deviceIds;

        // Apply the case's scope to the current scope state
        var fromUtc = forensicCase.ScopeFromUtc ?? _scopeState.Current.FromUtc;
        var toUtc = forensicCase.ScopeToUtc ?? _scopeState.Current.ToUtc;
        _scopeState.Set(_scopeState.Current with
        {
            DeviceIds = deviceIds,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Search = null  // Clear search when switching cases
        });

        OnChange?.Invoke();
        return true;
    }

    /// <summary>
    /// Deactivates the currently-active case.
    /// </summary>
    public void Deactivate()
    {
        if (ActiveCase is null)
        {
            return;
        }

        ActiveCase = null;
        ActiveCaseDeviceIds = new List<Guid>();
        OnChange?.Invoke();
    }

    /// <summary>
    /// Returns true if the current scope (devices/window) differs from the active case's saved scope.
    /// </summary>
    public bool ScopeDiffersFromCase
    {
        get
        {
            if (ActiveCase is null)
            {
                return false;
            }

            var caseFromUtc = ActiveCase.ScopeFromUtc ?? _scopeState.Current.FromUtc;
            var caseToUtc = ActiveCase.ScopeToUtc ?? _scopeState.Current.ToUtc;

            var devicesDiffer = !_scopeState.Current.DeviceIds.SequenceEqual(ActiveCaseDeviceIds);
            var windowDiffers = _scopeState.Current.FromUtc != caseFromUtc || _scopeState.Current.ToUtc != caseToUtc;

            return devicesDiffer || windowDiffers;
        }
    }

    /// <summary>
    /// Saves the current scope (devices and time window) to the active case.
    /// Throws InvalidOperationException if no case is active or if the case is closed.
    /// </summary>
    public async Task SaveScopeToCaseAsync(string actor, CancellationToken ct)
    {
        if (ActiveCase is null)
        {
            throw new InvalidOperationException("No active case to save scope to.");
        }

        if (ActiveCase.Status == CaseStatus.Closed)
        {
            throw new InvalidOperationException("Cannot modify a closed case.");
        }

        await _caseRepository.SetScopeAsync(
            ActiveCase.Id,
            _scopeState.Current.FromUtc,
            _scopeState.Current.ToUtc,
            _scopeState.Current.DeviceIds,
            actor,
            ct);

        // Refresh the active case to reflect the saved changes
        var refreshed = await _caseRepository.GetAsync(ActiveCase.Id, ct);
        if (refreshed is not null)
        {
            ActiveCase = refreshed;
            ActiveCaseDeviceIds = await _caseRepository.GetDeviceIdsAsync(ActiveCase.Id, ct);
        }
    }

    /// <summary>
    /// Pins an evidence item (event or media) to the active case with a given reason.
    /// Throws InvalidOperationException if no case is active or if the case is closed.
    /// </summary>
    public async Task<CaseItem> PinAsync(CaseItemKind kind, Guid targetId, string reason, string actor, CancellationToken ct)
    {
        if (ActiveCase is null)
        {
            throw new InvalidOperationException("No active case to pin evidence to.");
        }

        if (ActiveCase.Status == CaseStatus.Closed)
        {
            throw new InvalidOperationException("Cannot pin evidence to a closed case.");
        }

        return await _caseRepository.AddItemAsync(
            ActiveCase.Id,
            kind,
            targetId,
            reason,
            actor,
            ct);
    }
}
