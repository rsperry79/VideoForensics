using System;
using System.IO;
using System.Linq;

namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// The on-disk layout of downloaded media: <c>&lt;media root&gt;\&lt;provider&gt;\&lt;location&gt;\&lt;device&gt;</c>. The pull
    /// service writes new media here and the startup migration moves legacy media here, so both must build the
    /// same folder from the same names.
    /// </summary>
    public static class MediaFolderLayout
    {
        // Fixed Windows-reserved set, not Path.GetInvalidFileNameChars, so segments are the same on every platform
        // (on Linux that API returns only '/' and NUL).
        private static readonly char[] InvalidSegmentChars = Enumerable.Range(0x00, 0x20)
            .Select(c => (char)c)
            .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
            .ToArray();

        /// <summary>
        /// Builds the folder for one device. Each name is sanitized, lowercased, and has spaces replaced with
        /// underscores, so provider "Ring", location "Arlington" and device "Front Door" give <c>ring\arlington\front_door</c>.
        /// </summary>
        public static string GetDeviceFolder(string mediaRoot, string providerName, string locationName, string deviceName)
        {
            return Path.Combine(
                mediaRoot,
                Segment(providerName),
                Segment(locationName),
                Segment(deviceName));
        }

        private static string Segment(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "unknown";
            }

            string sanitized = new string(name.Where(c => !InvalidSegmentChars.Contains(c)).ToArray()).Trim();
            if (sanitized.Length == 0)
            {
                return "unknown";
            }

            return sanitized.ToLowerInvariant().Replace(' ', '_');
        }
    }
}
