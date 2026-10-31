using Microsoft.Extensions.Logging;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>Logs the Ring values that were read but not stored, once per device per sync, naming the device (never its id).</summary>
    public static class RingUnstoredValueLog
    {
        /// <summary>Writes one Info line listing each note (key, reason and JSON type only; never the value). No-op when there are none.</summary>
        public static void Log(ILogger logger, string deviceName, RingDeviceDetails details)
        {
            if (details.UnstoredNotes is not { Count: > 0 } notes)
            {
                return;
            }

            logger.LogInformation("Ring values not stored for device {DeviceName}: {Notes}", deviceName, string.Join("; ", notes));
        }
    }
}
