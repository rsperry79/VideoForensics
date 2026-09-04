namespace VideoForensics.Providers.Ring.Services;

/// <summary>Provides access to the shared Ring SDK Session instance</summary>
public interface ISessionProvider
{
    /// <summary>
    /// Gets the current Session instance, or null if not authenticated.
    /// Back-compat convenience for single-tenant hosts (console, MCP, and a planned MAUI client):
    /// operates on whichever account was most recently set via either <see cref="SetSession(Session)"/>
    /// or <see cref="SetSession(Guid, Session)"/>, so existing single-account call sites continue to
    /// work unchanged.
    /// </summary>
    Session? GetSession();

    /// <summary>
    /// Sets the Session instance (called by RingAuthService after authentication).
    /// Back-compat convenience for single-tenant hosts (console, MCP, and a planned MAUI client):
    /// this becomes "whichever account was most recently set", so subsequent parameterless
    /// <see cref="GetSession()"/>/<see cref="ClearSession()"/> calls operate on it.
    /// </summary>
    void SetSession(Session session);

    /// <summary>
    /// Clears the Session instance.
    /// Back-compat convenience for single-tenant hosts (console, MCP, and a planned MAUI client):
    /// operates on whichever account was most recently set via either <see cref="SetSession(Session)"/>
    /// or <see cref="SetSession(Guid, Session)"/>.
    /// </summary>
    void ClearSession();

    /// <summary>
    /// Gets the Session instance for a specific Ring account, or null if that account has no
    /// active session. Supports multiple concurrently-active Ring account sessions, which is
    /// needed for a planned multi-tenant web host where concurrent browser circuits may be
    /// signed into different Ring accounts simultaneously.
    /// </summary>
    Session? GetSession(Guid providerAccountId);

    /// <summary>
    /// Sets the Session instance for a specific Ring account. Supports multiple concurrently-active
    /// Ring account sessions, which is needed for a planned multi-tenant web host where concurrent
    /// browser circuits may be signed into different Ring accounts simultaneously.
    /// </summary>
    void SetSession(Guid providerAccountId, Session session);

    /// <summary>
    /// Clears the Session instance for a specific Ring account. Supports multiple
    /// concurrently-active Ring account sessions, which is needed for a planned multi-tenant web
    /// host where concurrent browser circuits may be signed into different Ring accounts
    /// simultaneously.
    /// </summary>
    void ClearSession(Guid providerAccountId);
}
