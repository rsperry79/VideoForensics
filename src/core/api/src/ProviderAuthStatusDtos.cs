using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire form of a provider's non-secret session status, returned by GET /api/v1/auth/status.
    /// Carries no access or refresh tokens.
    /// </summary>
    /// <param name="IsAuthenticated">True when the provider holds a usable session.</param>
    /// <param name="TokenType">Token scheme, for example "Bearer". Null when not authenticated or not reported.</param>
    /// <param name="ExpiresAtUtc">When the current token expires, in UTC. Null when unknown.</param>
    /// <param name="Scope">Scope the token was issued for, when reported.</param>
    public record ProviderAuthStatusDto(bool IsAuthenticated, string? TokenType, DateTime? ExpiresAtUtc, string? Scope);

    /// <summary>Extension methods for mapping <see cref="ProviderAuthStatus"/> to and from <see cref="ProviderAuthStatusDto"/>.</summary>
    public static class ProviderAuthStatusDtoMapping
    {
        /// <summary>Converts a provider status to its wire DTO.</summary>
        public static ProviderAuthStatusDto ToDto(this ProviderAuthStatus status)
        {
            return new ProviderAuthStatusDto(status.IsAuthenticated, status.TokenType, status.ExpiresAtUtc, status.Scope);
        }

        /// <summary>Converts a wire DTO back to the provider status.</summary>
        public static ProviderAuthStatus ToDomain(this ProviderAuthStatusDto dto)
        {
            return new ProviderAuthStatus(dto.IsAuthenticated, dto.TokenType, dto.ExpiresAtUtc, dto.Scope);
        }
    }
}
