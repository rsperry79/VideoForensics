using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IChatService"/> that calls the server's embedded MCP-based chat endpoint
    /// (see VideoForensics.WebApp/Api/ChatEndpoints.cs) instead of a local LLM. This is the client-side half of the
    /// client/server split (plan §4/M5): a thin MAUI client sends chat messages to the server, which runs the MCP
    /// tool-use loop and returns the assistant's reply.
    /// </summary>
    public class RemoteChatService : IChatService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteChatService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<ChatTurnResult> SendMessageAsync(IReadOnlyList<ChatTurn> history, string message, CancellationToken ct)
        {
            var request = new ChatRequestDto(
                History: history.Select(h => h.ToDto()).ToList(),
                Message: message
            );
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/chat", request, ct);
            _ = response.EnsureSuccessStatusCode();
            ChatResponseDto? dto = await response.Content.ReadFromJsonAsync<ChatResponseDto>(JsonOptions, ct);
            return dto?.ToDomain() ?? new ChatTurnResult { Reply = string.Empty, ToolsInvoked = [] };
        }
    }
}
