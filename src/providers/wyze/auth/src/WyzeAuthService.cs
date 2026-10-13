using System;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Wyze;

/// <summary>Implementation of Wyze authentication service</summary>
public class WyzeAuthService : IWyzeAuthService
{
    public Task<WyzeCredentials> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Wyze authentication service is not yet implemented");
    }

    public Task<WyzeCredentials> RefreshTokenAsync(WyzeCredentials credentials, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Wyze token refresh is not yet implemented");
    }

    public Task<bool> ValidateCredentialsAsync(WyzeCredentials credentials, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Wyze credential validation is not yet implemented");
    }
}
