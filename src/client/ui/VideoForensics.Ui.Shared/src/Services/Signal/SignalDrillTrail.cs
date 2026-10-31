using VideoForensics.Client.Ui.Shared.Services.Scope;

namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>
/// The scopes a signal-history drill-down came from, so the page can show a breadcrumb back to each level.
/// The trail only applies while the scope is still the one the last drill landed on. If the user changes the scope
/// from the right-hand panel, the trail is treated as stale and the breadcrumb hides.
/// </summary>
public sealed class SignalDrillTrail
{
    private readonly List<Crumb> _ancestors = new();
    private ForensicScope? _drilledTo;

    /// <summary>One level above the current view: the scope it came from, and the label shown for it.</summary>
    public sealed record Crumb(ForensicScope Scope, string Label);

    /// <summary>
    /// The levels above <paramref name="current"/>, outermost first. Empty when the view was not reached by drilling,
    /// or when the scope has changed since the last drill.
    /// </summary>
    public IReadOnlyList<Crumb> AncestorsOf(ForensicScope current)
    {
        return _drilledTo is not null && _drilledTo.Equals(current) ? _ancestors.ToList() : [];
    }

    /// <summary>
    /// Records a drill from <paramref name="from"/> (labelled <paramref name="fromLabel"/>) to <paramref name="applied"/>.
    /// Drilling again from a drilled view adds a level. Drilling from anywhere else starts a new trail.
    /// </summary>
    public void DrillInto(ForensicScope from, string fromLabel, ForensicScope applied)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(applied);

        if (_drilledTo is null || !_drilledTo.Equals(from))
        {
            _ancestors.Clear();
        }

        _ancestors.Add(new Crumb(from, fromLabel));
        _drilledTo = applied;
    }

    /// <summary>
    /// Goes back to the level at <paramref name="index"/> in <see cref="AncestorsOf"/>, dropping the levels below it.
    /// Returns the scope to apply. Restoring the outermost level clears the trail.
    /// </summary>
    public ForensicScope RestoreTo(int index)
    {
        if (index < 0 || index >= _ancestors.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "There is no breadcrumb level at that index.");
        }

        ForensicScope target = _ancestors[index].Scope;
        _ancestors.RemoveRange(index, _ancestors.Count - index);
        _drilledTo = _ancestors.Count == 0 ? null : target;
        return target;
    }
}
