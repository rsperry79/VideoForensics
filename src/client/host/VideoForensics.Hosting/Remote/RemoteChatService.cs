using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
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

        /// <inheritdoc />
        /// <remarks>
        /// Reads the server-sent event stream from <c>/api/v1/chat/stream</c> as it arrives. Headers are read before the
        /// body (<see cref="HttpCompletionOption.ResponseHeadersRead"/>) so deltas are yielded without waiting for the
        /// full reply. Unknown SSE event names are skipped so a newer server can add frames without breaking this client.
        /// </remarks>
        public async IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(
            IReadOnlyList<ChatTurn> history,
            string message,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var request = new ChatRequestDto(
                History: history.Select(h => h.ToDto()).ToList(),
                Message: message
            );

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat/stream")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            _ = response.EnsureSuccessStatusCode();

            await using Stream body = await response.Content.ReadAsStreamAsync(ct);

            await foreach (SseItem<string> sse in SseParser.Create(body).EnumerateAsync(ct))
            {
                ChatStreamEvent? evt = ToDomainEvent(sse);
                if (evt is null)
                {
                    continue;
                }

                yield return evt;

                if (evt is ChatTurnCompleted)
                {
                    yield break;
                }
            }
        }

        /// <summary>Maps one SSE frame to a domain event, or null for frames this client does not understand.</summary>
        private static ChatStreamEvent? ToDomainEvent(SseItem<string> sse)
        {
            switch (sse.EventType)
            {
                case "delta":
                    ChatStreamDeltaDto? delta = JsonSerializer.Deserialize<ChatStreamDeltaDto>(sse.Data, JsonOptions);
                    return delta is null ? null : new ChatTextDelta(delta.Text);

                case "tool":
                    ChatStreamToolDto? tool = JsonSerializer.Deserialize<ChatStreamToolDto>(sse.Data, JsonOptions);
                    return tool is null ? null : new ChatToolInvoked(tool.Name);

                case "done":
                    ChatResponseDto? done = JsonSerializer.Deserialize<ChatResponseDto>(sse.Data, JsonOptions);
                    return new ChatTurnCompleted(done?.ToDomain() ?? new ChatTurnResult { Reply = string.Empty, ToolsInvoked = [] });

                default:
                    return null;
            }
        }
    }
}
