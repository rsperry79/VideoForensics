using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring.Interfaces;

/// <summary>
/// High-level client for handling Ring authentication.
/// </summary>
public interface IAuthenticationClient
{
    /// <summary>
    /// Signs in to Ring with username and password.
    /// </summary>
    Task<bool> SignInAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes two-factor authentication.
    /// </summary>
    Task<bool> SignInWithTwoFactorAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the current authentication token.
    /// </summary>
    Task<bool> RefreshAuthenticationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs out and clears authentication.
    /// </summary>
    Task SignOutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the user is currently authenticated.
    /// </summary>
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
}
