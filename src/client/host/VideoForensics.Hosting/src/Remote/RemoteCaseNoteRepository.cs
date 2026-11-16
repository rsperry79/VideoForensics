using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>HTTP-backed <see cref="ICaseNoteRepository"/> that calls the server's case notes routes.</summary>
    public class RemoteCaseNoteRepository : ICaseNoteRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteCaseNoteRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<CaseNoteDto> AddAsync(Guid caseId, string body, string addedBy, CancellationToken ct)
        {
            var request = new AddCaseNoteRequest(body);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/api/v1/cases/{caseId}/notes", request, cancellationToken: ct);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                throw new ArgumentException(message, nameof(body));
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException("Case was not found.");
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(message);
            }

            _ = response.EnsureSuccessStatusCode();
            CaseNoteDto? dto = await response.Content.ReadFromJsonAsync<CaseNoteDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Server returned null response");
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<CaseNoteDto>> ListForCaseAsync(Guid caseId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/cases/{caseId}/notes", ct);
            _ = response.EnsureSuccessStatusCode();
            List<CaseNoteDto>? dtos = await response.Content.ReadFromJsonAsync<List<CaseNoteDto>>(JsonOptions, ct);
            return dtos ?? [];
        }
    }
}
