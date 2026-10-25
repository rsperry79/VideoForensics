using Microsoft.Extensions.Logging;

namespace VideoForensics.Client.Platform.Windows.Logging
{
    /// <summary>
    /// Adds the Windows Event Log provider, so unattended service runs are visible in Event Viewer.
    /// Core logging calls this through a delegate, so Core carries no Windows Event Log code.
    /// </summary>
    public static class WindowsLoggingProviders
    {
        /// <summary>Adds the Event Log provider with the "VideoForensics" source name.</summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static void AddEventLog(ILoggingBuilder logging)
        {
            _ = logging.AddEventLog(settings => settings.SourceName = "VideoForensics");
        }
    }
}
