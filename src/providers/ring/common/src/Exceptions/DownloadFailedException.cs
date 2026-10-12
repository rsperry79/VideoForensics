using System;

namespace VideoForensics.Providers.Ring.Exceptions
{
    /// <summary>
    /// Exception thrown when a download from the Ring API failed
    /// </summary>
    public class DownloadFailedException : Exception
    {
        /// <summary>
        /// The error message to return
        /// </summary>
        private const string errorMessage = "Downloading of the recorded Ring event from '{0}' failed";

        /// <summary>The HTTP status code Ring returned, when available, set via object initializer at the throw site.</summary>
        public System.Net.HttpStatusCode? StatusCode { get; init; }

        /// <summary>The raw response body Ring returned, truncated to ~4000 chars, when available.</summary>
        public string? ResponseBody { get; init; }

        public DownloadFailedException(string url) : base(string.Format(errorMessage, url))
        {
        }

        public DownloadFailedException(string url, System.Net.WebException innerException) : base(string.Format(errorMessage, url), innerException)
        {
        }
    }
}
