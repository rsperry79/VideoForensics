using System.Threading.Tasks;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// Service for handling authentication with the Ring API.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Gets a Session instance using a refresh token.
    /// </summary>
    Task<Session> GetSessionByRefreshToken(string refreshToken);

    /// <summary>
    /// Authenticates with the Ring API using stored credentials.
    /// </summary>
    Task<bool> Authenticate(string operatingSystem = "windows");

    /// <summary>
    /// Refreshes the current session using the refresh token.
    /// </summary>
    Task<bool> RefreshSession();

    /// <summary>
    /// Ensures the session is valid, refreshing if necessary.
    /// </summary>
    Task EnsureSessionValid();

    /// <summary>
    /// Gets whether the session is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}
