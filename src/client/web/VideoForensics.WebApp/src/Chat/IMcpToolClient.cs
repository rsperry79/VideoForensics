namespace VideoForensics.WebApp.Chat
{
    /// <summary>
    /// Minimal abstraction over McpClient's tool-listing and tool-invocation operations,
    /// to enable unit testing of ChatOrchestrator's tool-use loop without requiring complex mocking
    /// of the concrete ModelContextProtocol.Client.McpClient type.
    /// </summary>
    public interface IMcpToolClient
    {
        ValueTask<ModelContextProtocol.Protocol.ListToolsResult> ListToolsAsync(
            ModelContextProtocol.Protocol.ListToolsRequestParams request,
            CancellationToken ct);

        ValueTask<ModelContextProtocol.Protocol.CallToolResult> CallToolAsync(
            string toolName,
            IReadOnlyDictionary<string, object?>? toolArguments,
            CancellationToken ct);
    }
}
