using System;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Clients;

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

    public async Task<bool> SignInAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Username and password are required");
        }

        return await _authService.Authenticate(cancellationToken: cancellationToken);
    }

    public async Task<bool> SignInWithTwoFactorAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(code))
        {
            throw new ArgumentException("Two-factor code is required");
        }

        return await _authService.Authenticate(cancellationToken: cancellationToken);
    }

    public async Task<bool> RefreshAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        return await _authService.RefreshSession(cancellationToken);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        // Invalidate the session
        // This would be implemented in the actual Session class
        await Task.CompletedTask;
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        await _authService.EnsureSessionValid(cancellationToken);
        return _authService.IsAuthenticated;
    }
}
