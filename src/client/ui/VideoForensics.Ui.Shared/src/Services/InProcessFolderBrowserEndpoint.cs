using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>
    /// Folder browser endpoint for the web host. The file manager's requests are answered by
    /// <see cref="FolderBrowseProtocol"/> directly, with no network hop. Going over loopback HTTP would
    /// drop the signed-in user's cookies, so the check the circuit already passed is what authorizes these calls.
    /// </summary>
    public sealed class InProcessFolderBrowserEndpoint : IFolderBrowserEndpoint, IDisposable
    {
        private readonly HttpClient _client;

        /// <param name="browser">The local filesystem browser the protocol reads from and creates folders in.</param>
        /// <param name="logger">Records failed browse requests. Optional so callers that do not log can omit it.</param>
        public InProcessFolderBrowserEndpoint(IDirectoryBrowserService browser, ILogger<InProcessFolderBrowserEndpoint>? logger = null)
        {
            _client = new HttpClient(new InProcessFolderBrowserHandler(browser, logger ?? NullLogger<InProcessFolderBrowserEndpoint>.Instance))
            {
                BaseAddress = new Uri("http://in-process/"),
            };
        }

        /// <inheritdoc/>
        public HttpClient Client => _client;

        /// <inheritdoc/>
        public string Url => FolderBrowseProtocol.Route;

        /// <inheritdoc/>
        public void Dispose() => _client.Dispose();

        private sealed class InProcessFolderBrowserHandler : HttpMessageHandler
        {
            private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

            private readonly IDirectoryBrowserService _browser;
            private readonly ILogger<InProcessFolderBrowserEndpoint> _logger;

            public InProcessFolderBrowserHandler(IDirectoryBrowserService browser, ILogger<InProcessFolderBrowserEndpoint> logger)
            {
                _browser = browser;
                _logger = logger;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                FolderBrowseRequestDto? body = request.Content is null
                    ? null
                    : await request.Content.ReadFromJsonAsync<FolderBrowseRequestDto>(JsonOptions, cancellationToken);

                // A missing or unreadable body becomes an empty action, which the protocol rejects as not-supported.
                FolderBrowseResponseDto response;
                try
                {
                    response = FolderBrowseProtocol.Handle(body ?? new FolderBrowseRequestDto(string.Empty, null, null), _browser);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Answer with an error, never throw: an exception here ends the user's Blazor circuit.
                    // Type and message only, no exception object, per the logging rules.
                    _logger.LogError("Folder browse failed: {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
                    response = new FolderBrowseResponseDto(null, Array.Empty<FolderBrowseEntryDto>(), new FolderBrowseErrorDto("browse-failed", "The folder list could not be loaded."));
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(response, options: JsonOptions),
                };
            }
        }
    }
}
