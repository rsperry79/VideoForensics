using System.Collections.Concurrent;

namespace VideoForensics.Providers.Uniview;

/// <summary>Provides access to the shared Uniview authenticated client instance</summary>
public interface IUniviewSessionProvider
{
    /// <summary>
    /// Gets the current authenticated UniviewClient instance, or null if not logged in.
    /// Back-compat convenience for single-tenant hosts (console, MCP, and a planned MAUI client):
    /// operates on whichever account was most recently set via either <see cref="SetClient(UniviewClient)"/>
    /// or <see cref="SetClient(Guid, UniviewClient)"/>, so existing single-account call sites continue to
    /// work unchanged.
    /// </summary>
    UniviewClient? GetClient();

    /// <summary>
    /// Sets the authenticated UniviewClient instance (called by UniviewAuthService after authentication).
    /// Back-compat convenience for single-tenant hosts (console, MCP, and a planned MAUI client):
    /// this becomes "whichever account was most recently set", so subsequent parameterless
    /// <see cref="GetClient()"/>/<see cref="ClearClient()"/> calls operate on it.
    /// </summary>
    void SetClient(UniviewClient client);

    /// <summary>
    /// Clears the authenticated UniviewClient instance.
    /// Back-compat convenience for single-tenant hosts (console, MCP, and a planned MAUI client):
    /// operates on whichever account was most recently set via either <see cref="SetClient(UniviewClient)"/>
    /// or <see cref="SetClient(Guid, UniviewClient)"/>.
    /// </summary>
    void ClearClient();

    /// <summary>
    /// Gets the authenticated UniviewClient instance for a specific account, or null if that account has no
    /// active client. Supports multiple concurrently-active Uniview NVR sessions, which is
    /// needed for a planned multi-tenant web host where concurrent browser circuits may be
    /// signed into different Uniview NVRs simultaneously.
    /// </summary>
    UniviewClient? GetClient(Guid providerAccountId);

    /// <summary>
    /// Sets the authenticated UniviewClient instance for a specific account. Supports multiple concurrently-active
    /// Uniview NVR sessions, which is needed for a planned multi-tenant web host where concurrent
    /// browser circuits may be signed into different Uniview NVRs simultaneously.
    /// </summary>
    void SetClient(Guid providerAccountId, UniviewClient client);

    /// <summary>
    /// Clears the authenticated UniviewClient instance for a specific account. Supports
    /// multiple concurrently-active Uniview NVR sessions, which is needed for a planned multi-tenant web
    /// host where concurrent browser circuits may be signed into different Uniview NVRs simultaneously.
    /// </summary>
    void ClearClient(Guid providerAccountId);
}

/// <summary>Holds the shared UniviewClient instance(s) for all Uniview services</summary>
public class UniviewSessionProvider : IUniviewSessionProvider
{
    /// <summary>
    /// Well-known key used to store/retrieve the client set via the parameterless
    /// <see cref="SetClient(UniviewClient)"/> overload. Keeping this fixed avoids inventing a new
    /// "default account" concept beyond what this class already needs.
    /// </summary>
    private static readonly Guid DefaultAccountKey = Guid.Empty;

    private readonly ConcurrentDictionary<Guid, UniviewClient> _clients = new();

    /// <summary>
    /// Tracks the account id most recently set via either <see cref="SetClient(UniviewClient)"/> or
    /// <see cref="SetClient(Guid, UniviewClient)"/>, for the parameterless overloads to operate on.
    /// </summary>
    private Guid? _lastSetAccountId;

    public UniviewClient? GetClient()
    {
        return _lastSetAccountId.HasValue ? GetClient(_lastSetAccountId.Value) : null;
    }

    public void SetClient(UniviewClient client)
    {
        SetClient(DefaultAccountKey, client);
    }

    public void ClearClient()
    {
        if (_lastSetAccountId.HasValue)
        {
            ClearClient(_lastSetAccountId.Value);
        }
    }

    public UniviewClient? GetClient(Guid providerAccountId)
    {
        return _clients.TryGetValue(providerAccountId, out UniviewClient? client) ? client : null;
    }

    public void SetClient(Guid providerAccountId, UniviewClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _clients[providerAccountId] = client;
        _lastSetAccountId = providerAccountId;
    }

    public void ClearClient(Guid providerAccountId)
    {
        _ = _clients.TryRemove(providerAccountId, out _);

        // If the cleared account was the "last set" one, clear that tracking too so a subsequent
        // parameterless GetClient()/ClearClient() doesn't operate on a now-removed client.
        if (_lastSetAccountId == providerAccountId)
        {
            _lastSetAccountId = null;
        }
    }
}
