using System.Collections.Concurrent;

namespace VideoForensics.Providers.Ring.Services;

/// <summary>Holds the shared Ring SDK Session instance(s) for all Ring services</summary>
public class SessionProvider : ISessionProvider
{
    /// <summary>
    /// Well-known key used to store/retrieve the session set via the parameterless
    /// <see cref="SetSession(Session)"/> overload. Keeping this fixed avoids inventing a new
    /// "default account" concept beyond what this class already needs.
    /// </summary>
    private static readonly Guid DefaultAccountKey = Guid.Empty;

    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();

    /// <summary>
    /// Tracks the account id most recently set via either <see cref="SetSession(Session)"/> or
    /// <see cref="SetSession(Guid, Session)"/>, for the parameterless overloads to operate on.
    /// </summary>
    private Guid? _lastSetAccountId;

    public Session? GetSession() =>
        _lastSetAccountId.HasValue ? GetSession(_lastSetAccountId.Value) : null;

    public void SetSession(Session session) => SetSession(DefaultAccountKey, session);

    public void ClearSession()
    {
        if (_lastSetAccountId.HasValue)
        {
            ClearSession(_lastSetAccountId.Value);
        }
    }

    public Session? GetSession(Guid providerAccountId) =>
        _sessions.TryGetValue(providerAccountId, out var session) ? session : null;

    public void SetSession(Guid providerAccountId, Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _sessions[providerAccountId] = session;
        _lastSetAccountId = providerAccountId;
    }

    public void ClearSession(Guid providerAccountId)
    {
        _sessions.TryRemove(providerAccountId, out _);

        // If the cleared account was the "last set" one, clear that tracking too so a subsequent
        // parameterless GetSession()/ClearSession() doesn't operate on a now-removed session.
        if (_lastSetAccountId == providerAccountId)
        {
            _lastSetAccountId = null;
        }
    }
}
