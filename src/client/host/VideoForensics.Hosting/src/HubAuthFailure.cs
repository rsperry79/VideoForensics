using System.Net;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Classifies a SignalR failure as an authentication rejection (HTTP 401). A 401 means the paired-device token
    /// was refused, so retrying cannot succeed; the caller must surface AuthFailed instead of reconnecting.
    /// </summary>
    internal static class HubAuthFailure
    {
        /// <summary>
        /// Returns true when the exception or any inner exception carries HTTP 401. Falls back to message text
        /// because SignalR does not always preserve the status code.
        /// </summary>
        public static bool IsAuthFailure(Exception? error)
        {
            for (Exception? current = error; current is not null; current = current.InnerException)
            {
                if (current is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
                {
                    return true;
                }

                if (current.Message.Contains("401", StringComparison.OrdinalIgnoreCase) ||
                    current.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
