namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Non-secret details of a provider session, shown as the raw payload on the Query API page.
    /// Access and refresh tokens are deliberately absent: they must never reach a UI or a log.
    /// </summary>
    /// <param name="IsAuthenticated">True when the provider holds a usable session.</param>
    /// <param name="TokenType">Token scheme reported by the provider, for example "Bearer". Null when not authenticated.</param>
    /// <param name="ExpiresAtUtc">When the current token expires, in UTC. Null when unknown or not authenticated.</param>
    /// <param name="Scope">Scope the token was issued for, when the provider reports one.</param>
    public sealed record ProviderAuthStatus(
        bool IsAuthenticated,
        string? TokenType,
        DateTime? ExpiresAtUtc,
        string? Scope);
}
