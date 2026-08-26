namespace VideoForensics.Providers.Ring.Services;

/// <summary>Provides access to the shared Ring SDK Session instance</summary>
public interface ISessionProvider
{
    /// <summary>Gets the current Session instance, or null if not authenticated</summary>
    Session? GetSession();

    /// <summary>Sets the Session instance (called by RingAuthService after authentication)</summary>
    void SetSession(Session session);

    /// <summary>Clears the Session instance</summary>
    void ClearSession();
}
