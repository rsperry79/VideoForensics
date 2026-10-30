namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// Which popup in the review top bar is open. Only one popup is open at a time, so opening one closes the others.
/// The top bar's outside-click backdrop and its Escape handler both close through this object.
/// </summary>
/// <remarks>
/// Scoped with the page state so every popup reads the same open id. Raises <see cref="OnChange"/> only when the open
/// popup actually changes, so a no-op close does not re-render the bar.
/// </remarks>
public sealed class ReviewPopupState
{
    /// <summary>The camera selector popup id.</summary>
    public const string Camera = "camera";

    /// <summary>The Filter panel popup id.</summary>
    public const string Filter = "filter";

    /// <summary>The Actions menu popup id.</summary>
    public const string Actions = "actions";

    /// <summary>The pending tags list popup id.</summary>
    public const string Pending = "pending";

    /// <summary>Raised once after a change to the open popup.</summary>
    public event Action? OnChange;

    /// <summary>The id of the open popup, or null when none is open.</summary>
    public string? Open { get; private set; }

    /// <summary>True when the popup with <paramref name="id"/> is the open one.</summary>
    public bool IsOpen(string id)
    {
        return Open == id;
    }

    /// <summary>
    /// Opens the popup with <paramref name="id"/> and closes any other. Toggling the popup that is already open closes it.
    /// </summary>
    public void Toggle(string id)
    {
        Open = Open == id ? null : id;
        OnChange?.Invoke();
    }

    /// <summary>Closes whichever popup is open. Does nothing, and raises nothing, when none is open.</summary>
    public void Close()
    {
        if (Open is null)
        {
            return;
        }

        Open = null;
        OnChange?.Invoke();
    }

    /// <summary>
    /// Closes the popup with <paramref name="id"/> only when it is the open one. A completing action in one popup
    /// must not close a different popup the user has since opened.
    /// </summary>
    public void Close(string id)
    {
        if (Open != id)
        {
            return;
        }

        Open = null;
        OnChange?.Invoke();
    }
}
