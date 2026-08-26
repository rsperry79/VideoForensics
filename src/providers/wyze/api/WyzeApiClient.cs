using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Wyze;

/// <summary>Main Wyze API client for accessing camera functionality</summary>
public class WyzeApiClient
{
    private readonly IWyzeAuthService _authService;
    private WyzeCredentials? _credentials;

    public WyzeApiClient(IWyzeAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    /// <summary>Authenticates and initializes the API client</summary>
    public async Task InitializeAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        _credentials = await _authService.AuthenticateAsync(email, password, cancellationToken);
    }

    /// <summary>Gets available devices</summary>
    public async Task<IEnumerable<WyzeDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        if (_credentials == null)
        {
            throw new InvalidOperationException("API client is not initialized. Call InitializeAsync first.");
        }

        throw new NotImplementedException("Wyze device retrieval is not yet implemented");
    }
}
