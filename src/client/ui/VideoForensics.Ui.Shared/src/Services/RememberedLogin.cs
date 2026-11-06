namespace VideoForensics.Client.Ui.Shared.Services;

/// <summary>
/// The username kept in localStorage when Remember me is ticked on the sign-in page. It is a per-browser convenience:
/// it is removed by Remember me being unticked on a successful sign-in, and by any user-initiated sign-out.
/// </summary>
public static class RememberedLogin
{
    /// <summary>The localStorage key the remembered username is stored under.</summary>
    public const string StorageKey = "vf.login.remembered";
}
