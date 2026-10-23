using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// In-memory, per-process implementation of <see cref="IFailedLoginIpTracker"/>. Counting state is
    /// deliberately not persisted (a restart only gives an attacker one extra window); the resulting ban
    /// is written to the database so it survives restarts and shows in the Banned IPs list.
    /// </summary>
    /// <remarks>
    /// Registered as a singleton, but the banned-range repository and audit logger are scoped, so each
    /// ban write resolves them from a fresh scope rather than capturing them here.
    /// </remarks>
    public sealed class FailedLoginIpTracker : IFailedLoginIpTracker
    {
        /// <summary>
        /// CreatedByOperatorId written on auto-created bans. There is no Operators row for it and the
        /// column has no foreign key, so this fixed value marks "written by the system", not a person.
        /// </summary>
        public static readonly Guid SystemOperatorId = VideoForensics.Core.Api.Contracts.BannedIpRangeDtoMapping.SystemAutoBanOperatorId;

        // Idle windows are only swept once the map grows past this, so the common path stays O(1).
        private const int SweepThreshold = 1024;

        private readonly ConcurrentDictionary<string, IpWindow> _windows = new(StringComparer.Ordinal);
        private readonly TimeProvider _timeProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FailedLoginIpTracker> _logger;

        public FailedLoginIpTracker(TimeProvider timeProvider, IServiceScopeFactory scopeFactory, ILogger<FailedLoginIpTracker>? logger = null)
        {
            _timeProvider = timeProvider;
            _scopeFactory = scopeFactory;
            // Fully qualified: the internal NullLogger<T> in this namespace (BannedIpMatchService.cs) would otherwise shadow it.
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FailedLoginIpTracker>.Instance;
        }

        public async Task<bool> RecordLockoutAsync(string clientIp, Guid operatorId, LockoutPolicySettings policy, CancellationToken ct)
        {
            if (!policy.AutoBanEnabled)
            {
                return false;
            }

            if (!TryNormalizeBannableAddress(clientIp, out IPAddress? address, out string skipReason))
            {
                _logger.LogDebug("Auto-ban skipped: {Reason}", skipReason);
                return false;
            }

            // v1 is IPv4-only: an IPv6 edge peer could otherwise be banned, and no IPv6 edge ranges are known yet.
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                _logger.LogDebug("Auto-ban skipped for IPv6 address");
                return false;
            }

            string key = address.ToString();
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            var window = TimeSpan.FromMinutes(policy.AutoBanWindowMinutes);
            var duration = TimeSpan.FromMinutes(policy.AutoBanDurationMinutes);
            int threshold = Math.Max(1, policy.AutoBanDistinctAccountThreshold);

            if (_windows.Count > SweepThreshold)
            {
                Sweep(now, window);
            }

            (bool crossed, int distinctAccounts) = WithWindow(key, w =>
            {
                // Already auto-banned in this process: stay quiet until the suppression lapses, so a
                // burst of concurrent lockouts writes one ban rather than one per request.
                if (w.SuppressedUntilUtc is { } suppressedUntil && now < suppressedUntil)
                {
                    return (false, 0);
                }

                PruneOperators(w, now - window);
                // Keyed by operator so a repeat lockout of the same account updates its timestamp and is not counted twice.
                w.LastLockoutByOperator[operatorId] = now;

                int count = w.LastLockoutByOperator.Count;
                if (count < threshold)
                {
                    return (false, count);
                }

                w.LastLockoutByOperator.Clear();
                w.SuppressedUntilUtc = now + duration;
                return (true, count);
            });

            return crossed && await WriteBanAsync(key, address, now, duration, window, distinctAccounts, ct);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Retires the window under its lock, the same way <see cref="Sweep"/> does, so a caller that already
        /// holds a reference to it retries on a fresh entry instead of counting into a dropped one.
        /// </remarks>
        public Task ClearAsync(string clientIp, CancellationToken ct)
        {
            if (!IPAddress.TryParse(clientIp, out IPAddress? parsed))
            {
                return Task.CompletedTask;
            }

            string key = (parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed).ToString();
            if (_windows.TryRemove(key, out IpWindow? window))
            {
                lock (window.Gate)
                {
                    window.Retired = true;
                }
            }

            return Task.CompletedTask;
        }

        private async Task<bool> WriteBanAsync(string key, IPAddress address, DateTime now, TimeSpan duration, TimeSpan window, int distinctAccounts, CancellationToken ct)
        {
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                IBannedIpRangeRepository bannedRanges = scope.ServiceProvider.GetRequiredService<IBannedIpRangeRepository>();
                ISecurityAuditLogger auditLog = scope.ServiceProvider.GetRequiredService<ISecurityAuditLogger>();

                string cidr = CidrForHost(address);
                IReadOnlyList<BannedIpRange> active = await bannedRanges.GetActiveAsync(ct);
                if (active.Any(range => string.Equals(range.CidrRange, cidr, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("Auto-ban skipped: an active ban already covers it");
                    return false;
                }

                DateTime expiresAtUtc = now + duration;
                int windowMinutes = (int)window.TotalMinutes;
                await bannedRanges.AddAsync(new BannedIpRange
                {
                    Id = Guid.NewGuid(),
                    CidrRange = cidr,
                    Reason = $"Auto-ban: {distinctAccounts} accounts locked in {windowMinutes} min",
                    CreatedAtUtc = now,
                    CreatedByOperatorId = SystemOperatorId,
                    ExpiresAtUtc = expiresAtUtc
                }, ct);

                // Audit detail carries the address and the account count only, never account GUIDs.
                await auditLog.LogAsync(SecurityAuditEventTypes.AutoBanCreated, null, null, key,
                    $"Auto-banned {cidr} until {expiresAtUtc:yyyy-MM-dd HH:mm} UTC after {distinctAccounts} distinct accounts locked out within {windowMinutes} min",
                    isUrgent: true, ct);

                _logger.LogWarning("Auto-banned {ClientIp} after {AccountCount} distinct account lockouts", key, distinctAccounts);
                return true;
            }
            catch (Exception ex)
            {
                // Release the suppression so a later lockout can retry the write.
                _ = WithWindow(key, w => { w.SuppressedUntilUtc = null; return 0; });
                if (ex is OperationCanceledException)
                {
                    throw;
                }

                _logger.LogError(ex, "Auto-ban write failed for {ClientIp}", key);
                return false;
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> under the window's lock. A window swept concurrently is marked
        /// Retired and dropped from the map, so a caller that raced with the sweep retries on a fresh entry
        /// instead of counting into an orphan.
        /// </summary>
        private T WithWindow<T>(string key, Func<IpWindow, T> action)
        {
            while (true)
            {
                IpWindow window = _windows.GetOrAdd(key, static _ => new IpWindow());
                lock (window.Gate)
                {
                    if (window.Retired)
                    {
                        continue;
                    }

                    return action(window);
                }
            }
        }

        private void Sweep(DateTime now, TimeSpan window)
        {
            DateTime windowStart = now - window;
            foreach (KeyValuePair<string, IpWindow> entry in _windows)
            {
                IpWindow w = entry.Value;
                lock (w.Gate)
                {
                    PruneOperators(w, windowStart);
                    bool idle = w.LastLockoutByOperator.Count == 0 && (w.SuppressedUntilUtc is null || w.SuppressedUntilUtc <= now);
                    if (!idle)
                    {
                        continue;
                    }

                    w.Retired = true;
                    _ = _windows.TryRemove(entry);
                }
            }
        }

        private static void PruneOperators(IpWindow w, DateTime windowStart)
        {
            foreach (Guid stale in w.LastLockoutByOperator.Where(kv => kv.Value < windowStart).Select(kv => kv.Key).ToList())
            {
                _ = w.LastLockoutByOperator.Remove(stale);
            }
        }

        /// <summary>
        /// Maps a resolved client IP to a bannable address, or returns false with the reason it must be skipped.
        /// Skips: missing/"unknown", unparseable, loopback/unspecified (Local tier), and IPv4 Cloudflare edge
        /// (an unresolved tunnel peer, whose ban would hit every Internet-tier user). Uses the resolver's own range list.
        /// </summary>
        /// <remarks>
        /// IPv6 is rejected by the caller after normalization, since v1 auto-ban is IPv4-only (no IPv6 edge ranges exist).
        /// </remarks>
        private static bool TryNormalizeBannableAddress(string? clientIp, [NotNullWhen(true)] out IPAddress? address, out string reason)
        {
            address = null;
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(clientIp) || string.Equals(clientIp, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                reason = "no client address";
                return false;
            }

            if (!IPAddress.TryParse(clientIp, out IPAddress? parsed))
            {
                reason = "unparseable client address";
                return false;
            }

            IPAddress normalized = parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed;
            if (IPAddress.IsLoopback(normalized) || normalized.Equals(IPAddress.Any) || normalized.Equals(IPAddress.IPv6Any))
            {
                reason = "loopback or unspecified address";
                return false;
            }

            if (NetworkTierResolver.IsCloudflareEdgeIp(normalized))
            {
                reason = "Cloudflare edge address (unresolved client)";
                return false;
            }

            address = normalized;
            return true;
        }

        /// <summary>Exact single-host range: /32 for IPv4, /128 for IPv6. Never a wider prefix, so one bad actor cannot take a shared network with them.</summary>
        private static string CidrForHost(IPAddress address)
        {
            int prefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            return $"{address}/{prefix}";
        }

        private sealed class IpWindow
        {
            public readonly object Gate = new();
            public readonly Dictionary<Guid, DateTime> LastLockoutByOperator = [];
            public DateTime? SuppressedUntilUtc;
            public bool Retired;
        }
    }
}
