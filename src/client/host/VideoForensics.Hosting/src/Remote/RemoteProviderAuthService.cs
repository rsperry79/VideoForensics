using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IProviderAuthService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/AuthEndpoints.cs) to perform provider authentication.
    /// Supports both single-step and two-factor authentication flows. Methods that require
    /// client-side state persistence (credential caching, token refresh) throw
    /// <see cref="NotSupportedException"/>, as a thin client has no persistent credential store.
    /// </summary>
    public class RemoteProviderAuthService : IProviderAuthService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;
        private readonly string? _providerName;

        public RemoteProviderAuthService(HttpClient httpClient) : this(httpClient, providerName: null) { }

        public RemoteProviderAuthService(HttpClient httpClient, string? providerName)
        {
            _httpClient = httpClient;
            _providerName = providerName;
        }

        /// <inheritdoc />
        public async Task<AuthResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            var request = new LoginRequestDto(username, password, _providerName);

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    "/api/v1/auth/login",
                    request,
                    cancellationToken);

                _ = response.EnsureSuccessStatusCode();
                AuthResultDto? dto = await response.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions, cancellationToken);

                if (dto == null)
                {
                    return new AuthResult(
                        Success: false,
                        ErrorMessage: "Server returned null response"
                    );
                }

                // If 2FA is required, return a failed result explaining the caller must use AuthenticateWithTwoFactorAsync
                return dto.RequiresTwoFactor
                    ? new AuthResult(
                        Success: false,
                        ErrorMessage: "Two-factor authentication is required. Use AuthenticateWithTwoFactorAsync with the two-factor code provider."
                    )
                    : MapAuthResultDtoToDomain(dto);
            }
            catch (HttpRequestException ex)
            {
                return new AuthResult(
                    Success: false,
                    ErrorMessage: $"Authentication failed: {ex.Message}"
                );
            }
        }

        /// <inheritdoc />
        public async Task<AuthResult> AuthenticateWithTwoFactorAsync(
            string username,
            string password,
            Func<Task<string>> twoFactorAuthCodeProvider,
            CancellationToken cancellationToken = default)
        {
            // First, attempt the initial login to see if 2FA is required
            var loginRequest = new LoginRequestDto(username, password, _providerName);

            try
            {
                HttpResponseMessage loginResponse = await _httpClient.PostAsJsonAsync(
                    "/api/v1/auth/login",
                    loginRequest,
                    cancellationToken);

                _ = loginResponse.EnsureSuccessStatusCode();
                AuthResultDto? loginDto = await loginResponse.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions, cancellationToken);

                if (loginDto == null)
                {
                    return new AuthResult(
                        Success: false,
                        ErrorMessage: "Server returned null response"
                    );
                }

                // If 2FA is not required, the authentication succeeded (or failed for a reason other than 2FA)
                if (!loginDto.RequiresTwoFactor)
                {
                    return MapAuthResultDtoToDomain(loginDto);
                }

                // 2FA is required: get the code from the provider callback
                string twoFactorCode = await twoFactorAuthCodeProvider();

                // Submit the 2FA code
                var twoFactorRequest = new TwoFactorRequestDto(loginDto.AuthAttemptId!.Value, twoFactorCode);

                HttpResponseMessage twoFactorResponse = await _httpClient.PostAsJsonAsync(
                    "/api/v1/auth/login/two-factor",
                    twoFactorRequest,
                    cancellationToken);

                _ = twoFactorResponse.EnsureSuccessStatusCode();
                AuthResultDto? twoFactorDto = await twoFactorResponse.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions, cancellationToken);

                return twoFactorDto == null
                    ? new AuthResult(
                        Success: false,
                        ErrorMessage: "Server returned null response after two-factor authentication"
                    )
                    : MapAuthResultDtoToDomain(twoFactorDto);
            }
            catch (HttpRequestException ex)
            {
                return new AuthResult(
                    Success: false,
                    ErrorMessage: $"Two-factor authentication failed: {ex.Message}"
                );
            }
        }

        /// <inheritdoc />
        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not supported on a remote (MAUI client) authentication service - use the server's API directly, or this method isn't wired up yet.");
        }

        /// <inheritdoc />
        public Task<bool> RefreshAuthAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not supported on a remote (MAUI client) authentication service - use the server's API directly, or this method isn't wired up yet.");
        }

        /// <inheritdoc />
        public Task<bool> RestoreFromSavedCredentialsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not supported on a remote (MAUI client) authentication service - use the server's API directly, or this method isn't wired up yet.");
        }

        /// <inheritdoc />
        public Task<bool> RestoreFromSavedCredentialsAsync(Guid? providerAccountId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not supported on a remote (MAUI client) authentication service - use the server's API directly, or this method isn't wired up yet.");
        }

        // The last status the server returned. The synchronous methods read this cache, because they cannot wait on HTTP
        // without blocking the UI thread. Call GetAuthStatusDetailsAsync first to refresh it.
        private ProviderAuthStatus? _lastStatus;
        private bool _statusFetched;

        /// <inheritdoc />
        public async Task<ProviderAuthStatus?> GetAuthStatusDetailsAsync(CancellationToken cancellationToken = default)
        {
            string route = "api/v1/auth/status";
            if (!string.IsNullOrEmpty(_providerName))
            {
                route += "?provider=" + Uri.EscapeDataString(_providerName);
            }

            ProviderAuthStatusDto? dto = await _httpClient.GetFromJsonAsync<ProviderAuthStatusDto>(route, cancellationToken);
            _lastStatus = dto?.ToDomain();
            _statusFetched = true;
            return _lastStatus;
        }

        /// <inheritdoc />
        /// <remarks>Returns the last status fetched by <see cref="GetAuthStatusDetailsAsync"/>, or null before any fetch.</remarks>
        public ProviderAuthStatus? GetAuthStatusDetails()
        {
            return _lastStatus;
        }

        /// <inheritdoc />
        /// <remarks>Derived from the last status fetched by <see cref="GetAuthStatusDetailsAsync"/>.</remarks>
        public string GetAuthStatus()
        {
            if (!_statusFetched)
            {
                return "Not checked yet";
            }

            return _lastStatus?.IsAuthenticated == true ? "Authenticated" : "Not authenticated";
        }

        /// <summary>
        /// Maps an <see cref="AuthResultDto"/> (HTTP response) to an <see cref="AuthResult"/> (domain model).
        /// </summary>
        private static AuthResult MapAuthResultDtoToDomain(AuthResultDto dto)
        {
            return new AuthResult(
                Success: dto.Success,
                ErrorMessage: dto.ErrorMessage,
                AuthToken: dto.AuthToken,
                ExpiresAt: dto.ExpiresAt,
                ProviderAccountId: dto.ProviderAccountId
            );
        }
    }
}
