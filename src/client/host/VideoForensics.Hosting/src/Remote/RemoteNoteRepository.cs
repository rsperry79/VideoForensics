using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="INoteRepository"/> that calls the server's generic notes routes
    /// (see VideoForensics.WebApp/Api/NoteEndpoints.cs) for jamming incidents and media flags.
    ///
    /// Notes are append-only. The server takes the author from the paired-device bearer token, so the
    /// addedBy argument of <see cref="AddAsync"/> is not sent; it is kept for interface compatibility.
    /// </summary>
    public class RemoteNoteRepository : INoteRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteNoteRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<Note> AddAsync(NoteTargetKind kind, Guid targetId, string body, string addedBy, CancellationToken ct)
        {
            var request = new AddNoteRequest(body);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/api/v1/notes/{kind}/{targetId}", request, cancellationToken: ct);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                throw new ArgumentException(message, nameof(body));
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException("Note target was not found.");
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(message);
            }

            _ = response.EnsureSuccessStatusCode();
            NoteDto? dto = await response.Content.ReadFromJsonAsync<NoteDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Server returned null response")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Note>> ListAsync(NoteTargetKind kind, Guid targetId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/notes/{kind}/{targetId}", ct);
            _ = response.EnsureSuccessStatusCode();
            List<NoteDto>? dtos = await response.Content.ReadFromJsonAsync<List<NoteDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }
    }
}
