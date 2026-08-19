using System.Threading.Tasks;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// High-level client for handling Ring authentication.
/// </summary>
public interface IAuthenticationClient
{
    /// <summary>
    /// Signs in to Ring with username and password.
    /// </summary>
    Task<bool> SignInAsync(string username, string password);

    /// <summary>
    /// Completes two-factor authentication.
    /// </summary>
    Task<bool> SignInWithTwoFactorAsync(string code);

    /// <summary>
    /// Refreshes the current authentication token.
    /// </summary>
    Task<bool> RefreshAuthenticationAsync();

    /// <summary>
    /// Signs out and clears authentication.
    /// </summary>
    Task SignOutAsync();

    /// <summary>
    /// Gets whether the user is currently authenticated.
    /// </summary>
    Task<bool> IsAuthenticatedAsync();
}
