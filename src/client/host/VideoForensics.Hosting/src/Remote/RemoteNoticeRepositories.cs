using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="INoticeRepository"/> that calls <c>/api/v1/notices</c>. The server takes the operator and
    /// role from the paired-device bearer token, so the operatorId and isAdmin arguments are not sent.
    /// </summary>
    public sealed class RemoteNoticeRepository : INoticeRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteNoticeRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<NoticeDto>> ListForOperatorAsync(Guid operatorId, bool isAdmin, bool includeDismissed, CancellationToken ct)
        {
            string path = $"/api/v1/notices?includeDismissed={(includeDismissed ? "true" : "false")}";
            HttpResponseMessage response = await _httpClient.GetAsync(path, ct);
            _ = response.EnsureSuccessStatusCode();
            List<NoticeDto>? dtos = await response.Content.ReadFromJsonAsync<List<NoticeDto>>(JsonOptions, ct);
            return dtos ?? [];
        }

        /// <inheritdoc />
        public async Task<int> CountUndismissedForOperatorAsync(Guid operatorId, bool isAdmin, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/notices/undismissed-count", ct);
            _ = response.EnsureSuccessStatusCode();
            UndismissedNoticeCountDto? count = await response.Content.ReadFromJsonAsync<UndismissedNoticeCountDto>(JsonOptions, ct);
            return count?.Count ?? 0;
        }
    }

    /// <summary>HTTP-backed <see cref="INoticeDismissalRepository"/> that calls <c>/api/v1/notices</c> dismiss routes.</summary>
    public sealed class RemoteNoticeDismissalRepository : INoticeDismissalRepository
    {
        private readonly HttpClient _httpClient;

        public RemoteNoticeDismissalRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task DismissAsync(Guid noticeId, Guid operatorId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsync($"/api/v1/notices/{noticeId}/dismiss", content: null, ct);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task DismissAllAsync(IEnumerable<Guid> noticeIds, Guid operatorId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/notices/dismiss-all", noticeIds.ToArray(), cancellationToken: ct);
            _ = response.EnsureSuccessStatusCode();
        }
    }
}
