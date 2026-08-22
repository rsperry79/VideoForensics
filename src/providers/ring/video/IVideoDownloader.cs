using System.IO;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Downloads recording bytes from an already-resolved URL (see <see cref="Session.GetDoorbotHistoryRecordingInfo(string)"/>
    /// for how that URL is obtained) - opening a stream over them or saving them to disk. Knows
    /// nothing about the Ring API itself, so it has no dependency on authentication, sessions, or
    /// any Ring-specific request signing.
    /// </summary>
    public interface IVideoDownloader
    {
        /// <summary>
        /// Downloads the recording at <paramref name="url"/> and returns it as an in-memory stream.
        /// </summary>
        Task<Stream> OpenStreamAsync(string url);

        /// <summary>
        /// Downloads the recording at <paramref name="url"/> and saves it to <paramref name="saveAsPath"/>.
        /// </summary>
        Task DownloadToFileAsync(string url, string saveAsPath);
    }
}
