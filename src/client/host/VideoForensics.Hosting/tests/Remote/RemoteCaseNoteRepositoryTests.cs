using System.Net;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Hosting.Remote;

namespace VideoForensics.Client.Hosting.Tests.Remote
{
    public class RemoteCaseNoteRepositoryTests
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
        public async Task RemoteCaseNoteRepository_Add_MapsCreatedNote()
        {
            Guid caseId = Guid.NewGuid();
            var dto = new CaseNoteDto(Guid.NewGuid(), caseId, "Observed motion at 14:02.", "operator-1", DateTime.UtcNow);
            string json = JsonSerializer.Serialize(dto, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var repo = new RemoteCaseNoteRepository(CreateHttpClientWithHandler(handler));

            VideoForensics.Data.Common.Entities.Note result = await repo.AddAsync(
                caseId, "Observed motion at 14:02.", "ignored-actor", CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal($"/api/v1/cases/{caseId}/notes", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal(dto.Id, result.Id);
            Assert.Equal(VideoForensics.Data.Common.Entities.NoteTargetKind.Case, result.TargetKind);
            Assert.Equal(caseId, result.TargetId);
            Assert.Equal("Observed motion at 14:02.", result.Body);
            Assert.Equal("operator-1", result.AddedBy);
        }

        [Fact]
        public async Task RemoteCaseNoteRepository_Add_Conflict_ThrowsInvalidOperation()
        {
            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("Case is closed.", System.Text.Encoding.UTF8, "text/plain")
                };
            });

            var repo = new RemoteCaseNoteRepository(CreateHttpClientWithHandler(handler));

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repo.AddAsync(Guid.NewGuid(), "Note", "ignored-actor", CancellationToken.None));
        }

        [Fact]
        public async Task RemoteCaseNoteRepository_Add_BadRequest_ThrowsArgument()
        {
            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Body is required.", System.Text.Encoding.UTF8, "text/plain")
                };
            });

            var repo = new RemoteCaseNoteRepository(CreateHttpClientWithHandler(handler));

            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                repo.AddAsync(Guid.NewGuid(), "   ", "ignored-actor", CancellationToken.None));
        }
    }
}
