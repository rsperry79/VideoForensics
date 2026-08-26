using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Wyze;

/// <summary>Wyze authentication service contract</summary>
public interface IWyzeAuthService
{
    /// <summary>Authenticates with Wyze using provided credentials</summary>
    Task<WyzeCredentials> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Refreshes the authentication token</summary>
    Task<WyzeCredentials> RefreshTokenAsync(WyzeCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>Validates if the provided credentials are still valid</summary>
    Task<bool> ValidateCredentialsAsync(WyzeCredentials credentials, CancellationToken cancellationToken = default);
}
