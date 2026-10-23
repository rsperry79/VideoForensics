namespace VideoForensics.Hosting
{
    /// <summary>
    /// Publishes DNS-01 challenge records through the Cloudflare API for one zone. The API token and zone id
    /// are fixed at construction, so a publisher can only touch the zone it was built for.
    /// </summary>
    public sealed class CloudflareDnsChallengePublisher : IDnsChallengePublisher
    {
        private readonly ICloudflareApiClient _cloudflare;
        private readonly string _apiToken;
        private readonly string _zoneId;

        public CloudflareDnsChallengePublisher(ICloudflareApiClient cloudflare, string apiToken, string zoneId)
        {
            _cloudflare = cloudflare;
            _apiToken = apiToken;
            _zoneId = zoneId;
        }

        public async Task<string> PublishTxtAsync(string recordName, string value, CancellationToken ct)
        {
            CloudflareRecordResult result = await _cloudflare.CreateTxtRecordAsync(_apiToken, _zoneId, recordName, value, ct);
            return result.IsSuccess && result.RecordId is not null
                ? result.RecordId
                : throw new InvalidOperationException("Could not publish the DNS challenge record: " + (result.ErrorMessage ?? "unknown error"));
        }

        public async Task RemoveAsync(string recordId, CancellationToken ct)
        {
            CloudflareOperationResult result = await _cloudflare.DeleteDnsRecordAsync(_apiToken, _zoneId, recordId, ct);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException("Could not remove the DNS challenge record: " + (result.ErrorMessage ?? "unknown error"));
            }
        }
    }

    /// <summary>Pure rules for when a certificate needs issuing and which zone owns a hostname.</summary>
    public static class CertificateRenewalPolicy
    {
        /// <summary>Renew once the certificate has 30 days or less left, or when there is none.</summary>
        public static readonly TimeSpan RenewBefore = TimeSpan.FromDays(30);

        public static bool IsRenewalDue(DateTimeOffset? notAfter, DateTimeOffset now)
            => notAfter is null || notAfter.Value - now <= RenewBefore;

        /// <summary>
        /// Returns the zone that contains <paramref name="hostname"/>, choosing the longest matching name so that
        /// a delegated subdomain zone wins over its parent. Null when no zone the token can see covers it.
        /// </summary>
        public static CloudflareZone? FindZoneForHost(IEnumerable<CloudflareZone> zones, string hostname)
        {
            string host = hostname.Trim().TrimEnd('.').ToLowerInvariant();
            return zones
                .Where(zone => host == zone.Name.ToLowerInvariant() || host.EndsWith("." + zone.Name.ToLowerInvariant(), StringComparison.Ordinal))
                .OrderByDescending(zone => zone.Name.Length)
                .FirstOrDefault();
        }
    }
}
