namespace VideoForensics.Client.Ui.Shared.Services.Notices;

/// <summary>
/// The actions a notice offers in the notices dropdown and on the Notices page: reviewing a signal deviation or a broken
/// audit chain, and opening the device settings where its health polling can be turned off.
/// </summary>
public static class NoticeActions
{
    /// <summary>The event type of the 2 std dev alert raised by the server's health poll.</summary>
    public const string SignalDeviationEventType = "SignalDeviation";

    /// <summary>The event type raised when a camera stops answering. Its action path opens the account's sync page.</summary>
    public const string DeviceOfflineEventType = "DeviceOffline";

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
    /// The link to review a notice: a signal deviation opens the signal history on its camera around the alerted hour, and
    /// a broken audit chain opens the chain of custody page. Both are the notice's action path. Null for other notices,
    /// or when there is no path to open.
    /// </summary>
    public static string? ReviewLink(string eventType, string? actionPath)
    {
        bool reviewable = eventType is SignalDeviationEventType or ChainOfCustodyFailedEventType;
        return reviewable && !string.IsNullOrWhiteSpace(actionPath) ? actionPath : null;
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
