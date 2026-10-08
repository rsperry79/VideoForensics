using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteLogViewerServiceTests
    {
        private const string Token = "step-up-1";

        private static readonly LogQuery EmptyQuery = new(null, null, null);

        private static (RemoteLogViewerService Service, List<HttpRequestMessage> Seen) Create(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
        {
            var seen = new List<HttpRequestMessage>();
            var handler = new FakeHandler((req, ct) =>
            {
                seen.Add(req);
                return respond(req, ct);
            });
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            return (new RemoteLogViewerService(http), seen);
        }

        private static HttpResponseMessage Sse(string body) => new(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(body)))
            {
                Headers = { ContentType = new("text/event-stream") }
            }
        };

        private static string Frame(long seq, string message)
            => $"id: {seq}\ndata: {{\"sequence\":{seq},\"timestampUtc\":\"2026-01-01T00:00:00+00:00\",\"level\":\"Information\",\"category\":\"Cat\",\"message\":\"{message}\",\"exception\":null}}\n\n";

        [Fact]
        public async Task GetPageAsync_MapsJsonToLogPage()
        {
            var dto = new LogPageDto(
                new List<LogEntryDto> { new(7, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), "Error", "Cat", "boom", "ex") },
                LatestSequence: 9, Truncated: true);
            var (service, _) = Create((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(dto) });

            LogPage page = await service.GetPageAsync(EmptyQuery, Token, CancellationToken.None);

            Assert.Equal(9, page.LatestSequence);
            Assert.True(page.Truncated);
            LogEntry entry = Assert.Single(page.Entries);
            Assert.Equal(new LogEntry(7, dto.Entries[0].TimestampUtc, "Error", "Cat", "boom", "ex"), entry);
        }

        [Fact]
        public async Task GetPageAsync_BuildsEscapedQueryString()
        {
            var (service, seen) = Create((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LogPageDto(Array.Empty<LogEntryDto>(), 0, false))
            });
            const string search = "a b&c#d=Ã©â‚¬ %20";

            _ = await service.GetPageAsync(new LogQuery("Warning", search, 42, 100), Token, CancellationToken.None);

            HttpRequestMessage req = Assert.Single(seen);
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("/api/v1/logs", req.RequestUri!.AbsolutePath);
            var parsed = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
            Assert.Equal("Warning", parsed["minLevel"]);
            Assert.Equal(search, parsed["search"]);
            Assert.Equal("42", parsed["afterSequence"]);
            Assert.Equal("100", parsed["limit"]);
            Assert.Equal(4, parsed.Count);
            Assert.Equal(string.Empty, req.RequestUri.Fragment);
        }

        [Fact]
        public async Task GetPageAsync_OmitsUnsetQueryValues()
        {
            var (service, seen) = Create((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LogPageDto(Array.Empty<LogEntryDto>(), 0, false))
            });

            _ = await service.GetPageAsync(EmptyQuery, Token, CancellationToken.None);

            Assert.Equal(string.Empty, seen[0].RequestUri!.Query);
        }

        [Fact]
        public async Task GetPageAsync_SendsStepUpHeaderPerRequest()
        {
            var (service, seen) = Create((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LogPageDto(Array.Empty<LogEntryDto>(), 0, false))
            });

            _ = await service.GetPageAsync(EmptyQuery, "first-token", CancellationToken.None);
            _ = await service.GetPageAsync(EmptyQuery, "second-token", CancellationToken.None);

            Assert.Equal("first-token", Assert.Single(seen[0].Headers.GetValues("X-StepUp-Token")));
            Assert.Equal("second-token", Assert.Single(seen[1].Headers.GetValues("X-StepUp-Token")));
        }

        [Fact]
        public async Task GetPageAsync_DoesNotTouchDefaultRequestHeaders()
        {
            var handler = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LogPageDto(Array.Empty<LogEntryDto>(), 0, false))
            });
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteLogViewerService(http);

            _ = await service.GetPageAsync(EmptyQuery, "leaky", CancellationToken.None);

            Assert.False(http.DefaultRequestHeaders.Contains("X-StepUp-Token"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetPageAsync_EmptyToken_Throws(string? token)
        {
            var (service, seen) = Create((_, _) => new HttpResponseMessage(HttpStatusCode.OK));

            _ = await Assert.ThrowsAnyAsync<ArgumentException>(() => service.GetPageAsync(EmptyQuery, token!, CancellationToken.None));
            Assert.Empty(seen);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task StreamAsync_EmptyToken_Throws(string? token)
        {
            var (service, seen) = Create((_, _) => Sse(""));

            _ = await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            {
                await foreach (LogEntry _ in service.StreamAsync(EmptyQuery, token!, CancellationToken.None)) { }
            });
            Assert.Empty(seen);
        }

        [Fact]
        public async Task GetPageAsync_CancellingToken_CancelsTheHttpCall()
        {
            using var cts = new CancellationTokenSource();
            bool cancelledAtHandler = false;
            var (service, _) = Create((_, ct) =>
            {
                cts.Cancel();
                cancelledAtHandler = ct.IsCancellationRequested;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new LogPageDto(Array.Empty<LogEntryDto>(), 0, false))
                };
            });

            try { _ = await service.GetPageAsync(EmptyQuery, Token, cts.Token); }
            catch (OperationCanceledException) { }

            Assert.True(cancelledAtHandler);
        }

        [Fact]
        public async Task GetPageAsync_AlreadyCancelled_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var (service, _) = Create((_, ct) => { ct.ThrowIfCancellationRequested(); return new HttpResponseMessage(HttpStatusCode.OK); });

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetPageAsync(EmptyQuery, Token, cts.Token));
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task GetPageAsync_NonSuccess_ThrowsHttpRequestExceptionWithStatus(HttpStatusCode status)
        {
            var (service, _) = Create((_, _) => new HttpResponseMessage(status));

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetPageAsync(EmptyQuery, Token, CancellationToken.None));
            Assert.Equal(status, ex.StatusCode);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task StreamAsync_NonSuccess_ThrowsHttpRequestExceptionWithStatus(HttpStatusCode status)
        {
            var (service, _) = Create((_, _) => new HttpResponseMessage(status));

            var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await foreach (LogEntry _ in service.StreamAsync(EmptyQuery, Token, CancellationToken.None)) { }
            });
            Assert.Equal(status, ex.StatusCode);
        }

        [Fact]
        public async Task StreamAsync_ParsesFramesInOrderAndSkipsHeartbeats()
        {
            string body = Frame(1, "one") + ": heartbeat\n\n" + Frame(2, "two") + ": heartbeat\n\n" + Frame(3, "three");
            var (service, seen) = Create((_, _) => Sse(body));

            var entries = new List<LogEntry>();
            await foreach (LogEntry e in service.StreamAsync(EmptyQuery, Token, CancellationToken.None))
            {
                entries.Add(e);
            }

            Assert.Equal(new long[] { 1, 2, 3 }, entries.Select(e => e.Sequence));
            Assert.Equal(new[] { "one", "two", "three" }, entries.Select(e => e.Message));
            Assert.Equal("/api/v1/logs/stream", seen[0].RequestUri!.AbsolutePath);
            Assert.Equal("text/event-stream", Assert.Single(seen[0].Headers.Accept).MediaType);
        }

        [Fact]
        public async Task StreamAsync_SendsStepUpHeaderAndResumeParam()
        {
            var (service, seen) = Create((_, _) => Sse(""));

            await foreach (LogEntry _ in service.StreamAsync(new LogQuery("Error", "x y&z", 55, 0), "stream-token", CancellationToken.None)) { }

            HttpRequestMessage req = Assert.Single(seen);
            Assert.Equal("stream-token", Assert.Single(req.Headers.GetValues("X-StepUp-Token")));
            var parsed = System.Web.HttpUtility.ParseQueryString(req.RequestUri!.Query);
            Assert.Equal("55", parsed["afterSequence"]);
            Assert.Equal("x y&z", parsed["search"]);
            Assert.Equal("Error", parsed["minLevel"]);
            Assert.False(req.Headers.Contains("Last-Event-ID"));
        }

        [Fact]
        public async Task StreamAsync_CancellingToken_StopsEnumeration()
        {
            using var cts = new CancellationTokenSource();
            var (service, _) = Create((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new HangingStream(Encoding.UTF8.GetBytes(Frame(1, "one"))))
                {
                    Headers = { ContentType = new("text/event-stream") }
                }
            });

            var received = new List<long>();
            Task consume = Task.Run(async () =>
            {
                await foreach (LogEntry e in service.StreamAsync(EmptyQuery, Token, cts.Token))
                {
                    received.Add(e.Sequence);
                    cts.Cancel();
                }
            });

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consume.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new long[] { 1 }, received);
        }

        [Fact]
        public void AddVideoForensicsClientApi_ResolvesILogViewerService()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(new Moq.Mock<Microsoft.JSInterop.IJSRuntime>().Object);
            services.AddSingleton<VideoForensics.Ui.Shared.Services.PairedSessionState>();
            services.AddVideoForensicsClientApi(new Uri("http://localhost:5000"));
            using ServiceProvider provider = services.BuildServiceProvider();

            ILogViewerService service = provider.GetRequiredService<ILogViewerService>();

            Assert.IsType<RemoteLogViewerService>(service);
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _respond;

            public FakeHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond) => _respond = respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_respond(request, cancellationToken));
        }

        /// <summary>Serves its bytes once, then blocks until the read is cancelled (an open, idle SSE connection).</summary>
        private sealed class HangingStream : Stream
        {
            private readonly MemoryStream _initial;

            public HangingStream(byte[] initial) => _initial = new MemoryStream(initial);

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                int n = _initial.Read(buffer.Span);
                if (n > 0) return n;
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
