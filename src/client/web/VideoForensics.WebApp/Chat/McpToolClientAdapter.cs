using ModelContextProtocol.Client;

namespace VideoForensics.WebApp.Chat
{
    /// <summary>
    /// Production adapter that wraps McpClient and delegates to its ListToolsAsync and CallToolAsync methods.
    /// Used to convert a real McpClient into the testable IMcpToolClient interface.
    /// </summary>
    internal sealed class McpToolClientAdapter : IMcpToolClient
    {
        private readonly McpClient _mcpClient;

        public McpToolClientAdapter(McpClient mcpClient)
        {
            _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
        }

        public ValueTask<ModelContextProtocol.Protocol.ListToolsResult> ListToolsAsync(
            ModelContextProtocol.Protocol.ListToolsRequestParams request,
            CancellationToken ct)
        {
            return _mcpClient.ListToolsAsync(request, ct);
        }

        public async ValueTask<ModelContextProtocol.Protocol.CallToolResult> CallToolAsync(
            string toolName,
            IReadOnlyDictionary<string, object?>? toolArguments,
            CancellationToken ct)
        {
            // McpClient.CallToolAsync takes (toolName, toolArguments, null, null, CancellationToken)
            return await _mcpClient.CallToolAsync(toolName, toolArguments, null, null, ct);
        }
    }
}
