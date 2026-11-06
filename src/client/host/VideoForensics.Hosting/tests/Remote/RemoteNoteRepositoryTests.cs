using System.Net;
using System.Text.Json;

using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests.Remote
{
    public class RemoteNoteRepositoryTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;
            public HttpRequestMessage? CapturedRequest { get; private set; }

            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                return await _responseFactory(request);
            }
        }

        private static HttpClient CreateHttpClientWithHandler(FakeHttpMessageHandler handler)
        {
            return new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        }

        [Fact]
        public async Task RemoteNoteRepository_Add_MapsCreatedNote()
        {
            var flagId = Guid.NewGuid();
            var dto = new NoteDto(Guid.NewGuid(), NoteTargetKind.MediaFlag, flagId, "Checked the clip.", "operator-1", DateTime.UtcNow);
            string json = JsonSerializer.Serialize(dto, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var repo = new RemoteNoteRepository(CreateHttpClientWithHandler(handler));

            Note result = await repo.AddAsync(
                NoteTargetKind.MediaFlag, flagId, "Checked the clip.", "ignored-actor", CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/notes/MediaFlag/{flagId}", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(dto.Id, result.Id);
            Assert.Equal(NoteTargetKind.MediaFlag, result.TargetKind);
            Assert.Equal(flagId, result.TargetId);
            Assert.Equal("Checked the clip.", result.Body);
            Assert.Equal("operator-1", result.AddedBy);
        }

        [Fact]
        public async Task RemoteNoteRepository_Add_Conflict_ThrowsInvalidOperation()
        {
            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("Case is closed.", System.Text.Encoding.UTF8, "text/plain")
                };
            });

            var repo = new RemoteNoteRepository(CreateHttpClientWithHandler(handler));

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repo.AddAsync(NoteTargetKind.Case, Guid.NewGuid(), "Note", "ignored-actor", CancellationToken.None));
        }

        [Fact]
        public async Task RemoteNoteRepository_Add_BadRequest_ThrowsArgument()
        {
            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Body is required.", System.Text.Encoding.UTF8, "text/plain")
                };
            });

            var repo = new RemoteNoteRepository(CreateHttpClientWithHandler(handler));

            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.AddAsync(NoteTargetKind.JammingIncident, Guid.NewGuid(), "   ", "ignored-actor", CancellationToken.None));
        }

        [Fact]
        public async Task RemoteNoteRepository_List_MapsOkNotes()
        {
            var incidentId = Guid.NewGuid();
            var dto = new NoteDto(Guid.NewGuid(), NoteTargetKind.JammingIncident, incidentId, "First note.", "operator-1", DateTime.UtcNow);
            string json = JsonSerializer.Serialize(new List<NoteDto> { dto }, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var repo = new RemoteNoteRepository(CreateHttpClientWithHandler(handler));

            IReadOnlyList<Note> result = await repo.ListAsync(NoteTargetKind.JammingIncident, incidentId, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/notes/JammingIncident/{incidentId}", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Note note = Assert.Single(result);
            Assert.Equal(dto.Id, note.Id);
            Assert.Equal(NoteTargetKind.JammingIncident, note.TargetKind);
            Assert.Equal("First note.", note.Body);
        }
    }
}
