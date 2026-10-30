namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>
    /// Where the folder picker sends its file manager requests. Each host registers one: the web host
    /// answers in-process, and MAUI calls the paired server over HTTP. The picker itself does not change
    /// between hosts; only this registration does.
    /// </summary>
    public interface IFolderBrowserEndpoint
    {
        /// <summary>The HTTP client the file manager uses. It must already carry the host's auth.</summary>
        HttpClient Client { get; }

        /// <summary>The protocol URL, relative to the base address of <see cref="Client"/>.</summary>
        string Url { get; }
    }
}
