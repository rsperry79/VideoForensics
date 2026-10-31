using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        public InProcessFolderBrowserEndpoint(IDirectoryBrowserService browser)
        {
            _client = new HttpClient(new InProcessFolderBrowserHandler(browser))
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

            public InProcessFolderBrowserHandler(IDirectoryBrowserService browser)
            {
                _browser = browser;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                FolderBrowseRequestDto? body = request.Content is null
                    ? null
                    : await request.Content.ReadFromJsonAsync<FolderBrowseRequestDto>(JsonOptions, cancellationToken);

                // A missing or unreadable body becomes an empty action, which the protocol rejects as not-supported.
                FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(body ?? new FolderBrowseRequestDto(string.Empty, null, null), _browser);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(response, options: JsonOptions),
                };
            }
        }
    }
}
