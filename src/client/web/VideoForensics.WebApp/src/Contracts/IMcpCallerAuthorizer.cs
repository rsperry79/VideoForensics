namespace VideoForensics.WebApp.Contracts
{
    /// <summary>
    /// Checks an MCP tool call's caller against a server authorization policy. MCP requests carry only baseline
    /// paired-device auth at the transport, so each tool that needs more calls this.
    /// </summary>
    public interface IMcpCallerAuthorizer
    {
        /// <summary>True when the caller of the current MCP request satisfies <paramref name="policyName"/>.</summary>
        Task<bool> IsAllowedAsync(string policyName);

        /// <summary>Throws <see cref="UnauthorizedAccessException"/> when the caller does not satisfy <paramref name="policyName"/>.</summary>
        Task RequireAsync(string policyName);
    }
}
