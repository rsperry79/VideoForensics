using VideoForensics.Client.Ui.Shared.Services;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// Folder browser endpoint for thin clients (MAUI). The file manager posts to the paired server's
    /// <c>/api/v1/filesystem/fileoperations</c> route (see VideoForensics.WebApp/Api/FolderBrowseEndpoints.cs),
    /// so the chosen folder is a path on the server, which is where storage and import operate.
    /// The client is the typed HttpClient registered in <see cref="VideoForensicsHostingExtensions"/>, so the
    /// paired-device bearer token is attached the same way as every other remote call.
    /// </summary>
    public sealed class RemoteFolderBrowserEndpoint : IFolderBrowserEndpoint
    {
        /// <param name="client">The paired server's HTTP client, with its base address and auth handler already set.</param>
        public RemoteFolderBrowserEndpoint(HttpClient client)
        {
            Client = client;
        }

        /// <inheritdoc/>
        public HttpClient Client { get; }

        /// <inheritdoc/>
        public string Url => FolderBrowseProtocol.Route;
    }
}
