namespace VideoForensics.Client.Ui.Shared.Services.Notices;

/// <summary>
/// The actions a notice offers in the notices dropdown and on the Notices page: creating a case from a signal deviation,
/// and opening the device settings where its health polling can be turned off.
/// </summary>
public static class NoticeActions
{
    /// <summary>The event type of the 2 std dev alert raised by the server's health poll.</summary>
    public const string SignalDeviationEventType = "SignalDeviation";

    /// <summary>The event type raised when a camera stops answering. Its action path opens the account's sync page.</summary>
    public const string DeviceOfflineEventType = "DeviceOffline";

    /// <summary>
    /// The new-case link for a signal deviation notice. The case opens with the notice's camera and day already selected
    /// (taken from the notice's action path) and the alert's details as its description. Null for other notices.
    /// </summary>
    public static string? CreateCaseLink(string eventType, string? actionPath, string? details)
    {
        if (eventType != SignalDeviationEventType || actionPath is null)
        {
            return null;
        }

        int query = actionPath.IndexOf('?', StringComparison.Ordinal);
        if (query < 0)
        {
            return null;
        }

        // The server appends the marker that keeps the alert from repeating; it is not part of the case description.
        string description = details ?? string.Empty;
        int marker = description.LastIndexOf(" [", StringComparison.Ordinal);
        if (marker >= 0)
        {
            description = description[..marker];
        }

        return $"/cases/new?{actionPath[(query + 1)..]}&title={Uri.EscapeDataString("Signal deviation")}&description={Uri.EscapeDataString(description)}";
    }

    /// <summary>The event type of an audit-trail failure. Its action path opens the chain of custody page to review it.</summary>
    public const string ChainOfCustodyFailedEventType = "ChainOfCustodyIntegrityFailed";

    /// <summary>
    /// The title shown for a notice, with spaces between the words of its event type:
    /// "SignalDeviation" becomes "Signal Deviation".
    /// </summary>
    public static string Title(string eventType)
    {
        if (string.IsNullOrEmpty(eventType))
        {
            return string.Empty;
        }

        var words = new System.Text.StringBuilder();
        for (int i = 0; i < eventType.Length; i++)
        {
            if (i > 0 && char.IsUpper(eventType[i]))
            {
                _ = words.Append(' ');
            }

            _ = words.Append(eventType[i]);
        }

        return words.ToString();
    }

    /// <summary>
    /// The link to review a broken audit chain: the chain of custody page the notice's action path names. Null for other
    /// notices, or when there is no path to open.
    /// </summary>
    public static string? ReviewLink(string eventType, string? actionPath)
    {
        return eventType == ChainOfCustodyFailedEventType && !string.IsNullOrWhiteSpace(actionPath) ? actionPath : null;
    }

    /// <summary>
    /// The link to the page where the notice's camera health polling can be turned off. Offline notices carry it as
    /// their action path. Null for other notices, or when the server found no account page to link to.
    /// </summary>
    public static string? DeviceSettingsLink(string eventType, string? actionPath)
    {
        return eventType == DeviceOfflineEventType && !string.IsNullOrWhiteSpace(actionPath) ? actionPath : null;
    }
}
