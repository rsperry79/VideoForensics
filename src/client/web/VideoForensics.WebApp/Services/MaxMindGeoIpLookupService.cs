using System.Net;
using MaxMind.GeoIP2;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// GeoIP lookup service using MaxMind's GeoLite2 database.
    /// Gracefully handles missing database files and lookup failures by returning null rather than throwing.
    /// </summary>
    public class MaxMindGeoIpLookupService : IGeoIpLookupService
    {
        private readonly string _dbFilePath;

        public MaxMindGeoIpLookupService(string dbFilePath)
        {
            _dbFilePath = dbFilePath;
        }

        public async Task<string?> LookupCountryCodeAsync(IPAddress? ipAddress, CancellationToken ct)
        {
            // Handle null or empty IP
            if (ipAddress == null)
            {
                return null;
            }

            try
            {
                // Check if database file exists
                if (!File.Exists(_dbFilePath))
                {
                    return null; // Database file not available, fail gracefully
                }

                // Use MaxMind's DatabaseReader to perform the lookup
                using var reader = new DatabaseReader(_dbFilePath);
                var country = reader.Country(ipAddress);
                return country?.Country?.IsoCode;
            }
            catch
            {
                // Any error (invalid DB format, corrupted file, etc.) - gracefully return null
                // Don't throw; let the caller (login handler) decide fail-open or fail-closed behavior
                return null;
            }
        }
    }
}
