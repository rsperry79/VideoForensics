using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring
{
    /// <inheritdoc cref="IVideoDownloader"/>
    public class VideoDownloader : IVideoDownloader, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly bool _ownHttpClient;

        /// <param name="httpClient">Optional HttpClient to use for downloads. Leave out to have one created internally.</param>
        public VideoDownloader(HttpClient httpClient = null)
        {
            if (httpClient != null)
            {
                _httpClient = httpClient;
                _ownHttpClient = false;
            }
            else
            {
                _httpClient = new HttpClient();
                _ownHttpClient = true;
            }
        }

        public async Task<Stream> OpenStreamAsync(string url)
        {
            var bytes = await DownloadBytesAsync(url);
            return new MemoryStream(bytes);
        }

        public async Task DownloadToFileAsync(string url, string saveAsPath)
        {
            var bytes = await DownloadBytesAsync(url);
            await File.WriteAllBytesAsync(saveAsPath, bytes);
        }

        private async Task<byte[]> DownloadBytesAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("A valid absolute URL is required to download a recording", nameof(url));
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            request.Headers.Range = new RangeHeaderValue(0, null);

            using var response = await _httpClient.SendAsync(request);
            return await response.Content.ReadAsByteArrayAsync();
        }

        public void Dispose()
        {
            if (_ownHttpClient)
            {
                _httpClient?.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
