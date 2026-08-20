using System.Threading;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// Service for handling authentication with the Ring API.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates with the Ring API using stored credentials.
    /// </summary>
    Task<bool> Authenticate(string operatingSystem = "windows", CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the current session using the refresh token.
    /// </summary>
    Task<bool> RefreshSession(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the session is valid, refreshing if necessary.
    /// </summary>
    Task EnsureSessionValid(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the session is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}
