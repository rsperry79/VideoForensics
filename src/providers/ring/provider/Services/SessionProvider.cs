namespace VideoForensics.Providers.Ring.Services;

/// <summary>Holds the shared Ring SDK Session instance for all Ring services</summary>
public class SessionProvider : ISessionProvider
{
    private Session? _session;

    public Session? GetSession() => _session;

    public void SetSession(Session session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void ClearSession()
    {
        _session = null;
    }
}
