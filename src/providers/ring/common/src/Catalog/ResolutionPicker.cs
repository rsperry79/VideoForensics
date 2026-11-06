using System;
using System.Collections.Generic;
using System.Globalization;

namespace VideoForensics.Providers.Ring.Catalog
{
    /// <summary>Chooses the largest resolution from "WIDTHxHEIGHT" strings. Compares pixel counts because text sorts "640x360" above "1920x1080".</summary>
    public static class ResolutionPicker
    {
        /// <summary>Returns the value with the most pixels, or null when no value parses. Values that do not parse are ignored.</summary>
        public static string? LargestByPixels(IEnumerable<string?> values)
        {
            string? best = null;
            long bestPixels = 0;
            foreach (string? value in values)
            {
                if (!TryPixels(value, out long pixels) || pixels <= bestPixels)
                {
                    continue;
                }

                best = value;
                bestPixels = pixels;
            }

            return best;
        }

        private static bool TryPixels(string? value, out long pixels)
        {
            pixels = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value.Trim().Split('x', 'X');
            return parts.Length == 2
                && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long width)
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long height)
                && width > 0
                && height > 0
                && (pixels = width * height) > 0;
        }
    }
}
