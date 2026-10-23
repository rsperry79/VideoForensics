using System.Globalization;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="ILogViewerService"/> calling <c>/api/v1/logs</c> and <c>/api/v1/logs/stream</c>
    /// (VideoForensics.WebApp/Api/LogEndpoints.cs). Maps the wire DTOs to the client-side log types at this boundary.
    /// </summary>
    public sealed class RemoteLogViewerService : ILogViewerService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;

        public RemoteLogViewerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<LogPage> GetPageAsync(LogQuery query, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(query);

            using HttpRequestMessage request = BuildRequest("/api/v1/logs", query);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
            _ = response.EnsureSuccessStatusCode();
            LogPageDto? dto = await response.Content.ReadFromJsonAsync<LogPageDto>(JsonOptions, ct);
            return (dto ?? new LogPageDto(Array.Empty<LogEntryDto>(), 0, false)).ToDomain();
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<LogEntry> StreamAsync(LogQuery query, [EnumeratorCancellation] CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(query);

            using HttpRequestMessage request = BuildRequest("/api/v1/logs/stream", query);
            request.Headers.Accept.Add(new("text/event-stream"));
            // Headers-only: the body is an endless stream and must not be buffered.
            using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            _ = response.EnsureSuccessStatusCode();
            await using Stream body = await response.Content.ReadAsStreamAsync(ct);

            // The parser surfaces only data frames; comment frames (heartbeats) are dropped.
            var parser = SseParser.Create(body, static (_, data) => JsonSerializer.Deserialize<LogEntryDto>(data, JsonOptions));
            await foreach (SseItem<LogEntryDto?> item in parser.EnumerateAsync(ct))
            {
                if (item.Data is { } dto)
                {
                    yield return dto.ToDomain();
                }
            }
        }

        private static HttpRequestMessage BuildRequest(string path, LogQuery query)
        {
            // Values are escaped individually: search text is user input and can contain '&', '#', spaces, etc.
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(query.MinLevel))
            {
                parts.Add("minLevel=" + Uri.EscapeDataString(query.MinLevel));
            }

            if (!string.IsNullOrEmpty(query.Search))
            {
                parts.Add("search=" + Uri.EscapeDataString(query.Search));
            }

            if (query.AfterSequence is { } after)
            {
                parts.Add("afterSequence=" + after.ToString(CultureInfo.InvariantCulture));
            }

            if (query.Limit > 0)
            {
                parts.Add("limit=" + query.Limit.ToString(CultureInfo.InvariantCulture));
            }

            string url = parts.Count == 0 ? path : path + "?" + string.Join('&', parts);
            return new HttpRequestMessage(HttpMethod.Get, url);
        }
    }

    /// <summary>Maps between the client-side log types and the wire DTOs.</summary>
    internal static class LogViewerMapping
    {
        public static LogEntry ToDomain(this LogEntryDto dto)
        {
            return new(dto.Sequence, dto.TimestampUtc, dto.Level, dto.Category, dto.Message, dto.Exception);
        }

        public static LogPage ToDomain(this LogPageDto dto)
        {
            return new(dto.Entries.Select(e => e.ToDomain()).ToList(), dto.LatestSequence, dto.Truncated);
        }

        public static LogQueryDto ToDto(this LogQuery query)
        {
            return new(query.MinLevel, query.Search, query.AfterSequence, query.Limit);
        }
    }
}
