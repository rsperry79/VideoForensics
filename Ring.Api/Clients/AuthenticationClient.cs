using System;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Interfaces;

namespace KoenZomers.Ring.Api.Clients;

/// <summary>
/// High-level client for authenticating with Ring.
/// </summary>
public class AuthenticationClient : IAuthenticationClient
{
    private readonly IAuthenticationService _authService;

    public AuthenticationClient(IAuthenticationService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<bool> SignInAsync(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Username and password are required");
        }

        return await _authService.Authenticate();
    }

    public async Task<bool> SignInWithTwoFactorAsync(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            throw new ArgumentException("Two-factor code is required");
        }

        return await _authService.Authenticate();
    }

    public async Task<bool> RefreshAuthenticationAsync()
    {
        return await _authService.RefreshSession();
    }

    public async Task SignOutAsync()
    {
        // Invalidate the session
        // This would be implemented in the actual Session class
        await Task.CompletedTask;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        await _authService.EnsureSessionValid();
        return _authService.IsAuthenticated;
    }
}
