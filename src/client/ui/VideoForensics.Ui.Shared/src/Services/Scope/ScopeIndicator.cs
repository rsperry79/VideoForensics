namespace VideoForensics.Ui.Shared.Services.Scope;

/// <summary>
/// Pure helper to determine if a forensic scope differs from the default.
/// No async, no side effects, no dependencies.
/// </summary>
public static class ScopeIndicator
{
    /// <summary>
    /// Returns true if the scope is custom (non-default): has devices selected,
    /// search set, or a time window different from the default 7-day window for the given date.
    /// </summary>
    public static bool IsCustom(ForensicScope current, DateTime utcNow)
    {
        var defaultScope = ForensicScope.Default(utcNow);
        return !current.Equals(defaultScope);
    }
}
